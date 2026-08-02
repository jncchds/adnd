using System.Text.Json;
using Adnd.Server.Data;
using Adnd.Server.Dtos;
using Adnd.Server.Events;
using Adnd.Server.Hubs;
using Adnd.Server.Models;
using Adnd.Server.Services.Llm;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Adnd.Server.Services;

/// <summary>Timeout message for AgentSaga — Id matches saga Id for Wolverine correlation.</summary>
public record SagaTimeout(Guid Id);

/// <summary>
/// Wolverine durable saga keyed on AgentCall.Id.
/// Tracks an agent call through LLM dispatch → tool execution → narrative phases.
/// A 5-minute timeout fires automatically to fail stuck sagas.
/// </summary>
public class AgentSaga : Wolverine.Saga
{
    public Guid Id { get; set; }
    public Guid AgentCallId { get; set; }
    public Guid GameId { get; set; }
    public int ToolsRemaining { get; set; }
    public Guid? CurrentToolId { get; set; }
    public string CurrentState { get; set; } = "Init";

    // Start() — triggered by AgentCallQueued. Wolverine routes a [SagaIdentity]-carrying
    // message solely to the saga's Start/Handle methods; a separate plain handler class for
    // the same message type is never invoked (silently — no exception), so the orchestration
    // that used to live in SagaOrchestratorHandler has to happen here.
    public async Task Start(AgentCallQueued msg, IMessageContext context, AppDbContext db, IGmActivityBroadcaster activity, ILogger<AgentSaga> logger)
    {
        Id = msg.AgentCallId;
        AgentCallId = msg.AgentCallId;
        GameId = msg.GameId;
        CurrentState = "Init";

        await context.ScheduleAsync(new SagaTimeout(Id), TimeSpan.FromMinutes(5));

        var call = await db.AgentCalls.FindAsync(msg.AgentCallId);
        if (call == null) return;
        call.Status = AgentCallStatus.Running;
        call.AdvanceStep(SagaStep.Init);
        await db.SaveChangesAsync();
        await activity.BroadcastAsync(msg.GameId, SagaStep.Init);

        string systemPrompt, userPrompt;
        try
        {
            var opts = string.IsNullOrEmpty(call.Input)
                ? null
                : JsonSerializer.Deserialize<GMDispatchOptions>(call.Input);
            systemPrompt = opts?.SystemPrompt ?? "";
            userPrompt = opts?.UserPrompt ?? "";
        }
        catch (JsonException ex)
        {
            // Previously swallowed: a malformed Input dispatched a billable LLM call with
            // two empty prompts and no indication anything had gone wrong.
            logger.LogError(ex, "Agent call {AgentCallId} has malformed Input; aborting dispatch", call.Id);
            await context.PublishAsync(new AgentCallFailed(call.Id, call.GameId, "Agent call input was not valid JSON."));
            return;
        }

        if (string.IsNullOrWhiteSpace(systemPrompt) && string.IsNullOrWhiteSpace(userPrompt))
        {
            logger.LogError("Agent call {AgentCallId} produced empty prompts; aborting dispatch", call.Id);
            await context.PublishAsync(new AgentCallFailed(call.Id, call.GameId, "Agent call produced an empty prompt."));
            return;
        }

        await context.PublishAsync(new LLMDispatchRequested(call.Id, call.GameId, systemPrompt, userPrompt));
    }

    // Transition on LLM response — move to tool execution or await narrative. This used to be
    // a void state-only transition while a separate LLMResponseHandler class did the real work
    // (advancing AgentCall.CurrentStep, publishing NarrativeReady/ToolCallRequested) — the same
    // saga-owned-message gotcha documented above for AgentCallQueued: Wolverine routes
    // LLMResponseReceived (it carries [SagaIdentity]) solely to this Handle method, so
    // LLMResponseHandler was silently never invoked and every narration hung forever at
    // CurrentStep=LLMResponse. The orchestration now lives here instead.
    public async Task Handle(LLMResponseReceived msg, IMessageContext context, AppDbContext db, ISessionManagementService sessions, IGmActivityBroadcaster activity)
    {
        var call = await db.AgentCalls.FindAsync(msg.AgentCallId);
        if (call == null) return;

        call.Output = msg.ResponseText;

        List<ToolCall> toolCalls = [];
        if (msg.HasToolCalls && !string.IsNullOrEmpty(msg.RawJson))
            toolCalls = JsonSerializer.Deserialize<List<ToolCall>>(msg.RawJson) ?? [];

        if (toolCalls.Count == 0)
        {
            CurrentState = "AwaitingNarrative";
            call.AdvanceStep(SagaStep.NarrativeReady);
            await db.SaveChangesAsync();
            await activity.BroadcastAsync(msg.GameId, SagaStep.NarrativeReady);

            var session = await sessions.GetOrCreateCurrentSessionAsync(msg.GameId);
            await context.PublishAsync(new NarrativeReady(msg.AgentCallId, msg.GameId, session.Id, msg.ResponseText));
            return;
        }

        // The GM deciding that nothing needs to happen is a legitimate outcome, not a failure:
        // players talking among themselves shouldn't force the world to react to every line.
        // Without a way to express that, the model had only two options — invent a beat it
        // didn't want to narrate, or return nothing, which the NarrativeReady guard below
        // turns into a retry and ultimately a red "GM error" for the whole table. A wait-only
        // response completes the turn here: no tools run, no follow-up LLM call is billed, no
        // Message row is written and nothing is broadcast except the Completed step that
        // clears the activity chip. Restricted to Narrate because the other actions are
        // explicit GM/admin requests for output, where silence would just look broken.
        if (call.Action == AgentAction.Narrate && toolCalls.All(t => t.Name == "wait"))
        {
            CurrentState = "Completed";
            call.Status = AgentCallStatus.Completed;
            call.AdvanceStep(SagaStep.Completed);
            call.DurationMs = (long)(DateTimeOffset.UtcNow - call.CreatedAt).TotalMilliseconds;

            // Kept for the admin LLM/agent-call views only — a turn that produced no output
            // is otherwise indistinguishable from one that broke halfway through.
            var reason = toolCalls
                .Select(t => t.Arguments.ValueKind == JsonValueKind.Object
                    && t.Arguments.TryGetProperty("reason", out var r) ? r.GetString() : null)
                .FirstOrDefault(r => !string.IsNullOrWhiteSpace(r));
            call.Output = string.IsNullOrWhiteSpace(reason)
                ? "GM waited — no action taken."
                : $"GM waited — {reason}";

            await db.SaveChangesAsync();
            await activity.BroadcastAsync(msg.GameId, SagaStep.Completed);
            MarkCompleted();
            return;
        }

        ToolsRemaining = toolCalls.Count;
        CurrentState = "ToolExecution";
        call.AdvanceStep(SagaStep.ToolExecution);
        await activity.BroadcastAsync(msg.GameId, SagaStep.ToolExecution);

        var coordinator = new ToolCallCoordinator
        {
            AgentCallId = msg.AgentCallId,
            TotalTools = toolCalls.Count,
            CompletedTools = 0,
            CurrentToolIndex = 0,
            ToolResults = JsonSerializer.SerializeToElement(new List<object>()),
            ToolCalls = JsonSerializer.SerializeToElement(toolCalls)
        };
        db.ToolCallCoordinators.Add(coordinator);
        await db.SaveChangesAsync();

        var first = toolCalls[0];
        await context.PublishAsync(new ToolCallRequested(msg.AgentCallId, msg.GameId, first.Name, first.Arguments.GetRawText(), 0));
    }

    // Advance the tool-call coordinator, dispatch the next tool, or move to the LLM follow-up.
    // Same saga-owned-message gotcha as above: this used to be a void state-only transition
    // while a separate CoordinatorHandler class (dead — Wolverine never invoked it) recorded
    // results, dispatched the next tool, and published LLMFollowUpRequested. Without it, any
    // GM response containing more than zero tool calls (dice rolls, skill checks, etc.) hung
    // forever after the first tool completed.
    public async Task Handle(ToolCallCompleted msg, IMessageContext context, AppDbContext db, IGmActivityBroadcaster activity)
    {
        ToolsRemaining = Math.Max(0, ToolsRemaining - 1);

        var coordinator = await db.ToolCallCoordinators.FirstOrDefaultAsync(c => c.AgentCallId == msg.AgentCallId);
        if (coordinator == null) return;

        var results = JsonSerializer.Deserialize<List<JsonElement>>(coordinator.ToolResults.GetRawText()) ?? [];
        results.Add(JsonSerializer.SerializeToElement(new { toolName = msg.ToolName, result = msg.ResultJson }));
        coordinator.ToolResults = JsonSerializer.SerializeToElement(results);
        coordinator.CompletedTools++;
        coordinator.CurrentToolIndex++;
        await db.SaveChangesAsync();

        var call = await db.AgentCalls.FindAsync(msg.AgentCallId);
        if (call == null) return;

        var toolCalls = coordinator.ToolCalls.ValueKind == JsonValueKind.Array
            ? JsonSerializer.Deserialize<List<ToolCall>>(coordinator.ToolCalls.GetRawText()) ?? []
            : [];

        if (coordinator.CurrentToolIndex < toolCalls.Count)
        {
            var next = toolCalls[coordinator.CurrentToolIndex];
            await context.PublishAsync(new ToolCallRequested(msg.AgentCallId, msg.GameId, next.Name, next.Arguments.GetRawText(), coordinator.CurrentToolIndex));
            return;
        }

        CurrentState = "LLMFollowUp";
        call.AdvanceStep(SagaStep.LLMFollowUp);
        await db.SaveChangesAsync();
        await activity.BroadcastAsync(msg.GameId, SagaStep.LLMFollowUp);

        string systemPrompt = "", userPrompt = "";
        if (!string.IsNullOrEmpty(call.Input))
        {
            try
            {
                var opts = JsonSerializer.Deserialize<GMDispatchOptions>(call.Input);
                systemPrompt = opts?.SystemPrompt ?? "";
                userPrompt = opts?.UserPrompt ?? "";
            }
            catch (JsonException)
            {
                // Dispatch already validated Input; a failure here is non-fatal.
            }
        }

        var toolSummary = string.Join("\n", results.Select(r => r.GetRawText()));
        await context.PublishAsync(new LLMFollowUpRequested(msg.AgentCallId, msg.GameId, systemPrompt, userPrompt, toolSummary));
    }

    // Narrative ready — save + broadcast the message and complete the saga. Same saga-owned-
    // message gotcha as above, for the third and most consequential time: NarrativeReady
    // carries [SagaIdentity], so the standalone NarrativeHandler class — which actually saved
    // the Message row, flipped AgentCall.Status to Completed, and pushed it over SignalR — was
    // silently never invoked. Every TriggerNarrate/TriggerSuggest call reached this point and
    // then simply vanished: no chat message, no completed status, saga stuck at "Running".
    public async Task Handle(NarrativeReady msg, IMessageContext context, AppDbContext db, IHubContext<GameHub> hub, IGmActivityBroadcaster activity)
    {
        // The LLM call can "succeed" (no exception, normal HTTP 200) yet still produce zero
        // narrative text — observed live with a reasoning model that spent its whole
        // completion budget on internal chain-of-thought and never wrote an actual answer.
        // Saving that as a Message produced a blank "Game Master" chat bubble with no
        // explanation. Treat it as a failure instead so the existing AgentCallFailedHandler
        // retry path (same prompt, often succeeds on a second pass) kicks in, rather than
        // completing the saga on nothing.
        if (string.IsNullOrWhiteSpace(msg.NarrativeText))
        {
            await context.PublishAsync(new AgentCallFailed(msg.AgentCallId, msg.GameId, "The GM's response came back empty."));
            return;
        }

        CurrentState = "Completed";

        var call = await db.AgentCalls.FindAsync(msg.AgentCallId);
        if (call != null)
        {
            call.Status = AgentCallStatus.Completed;
            call.AdvanceStep(SagaStep.Completed);
            call.OutputMessage = msg.NarrativeText;
            call.DurationMs = (long)(DateTimeOffset.UtcNow - call.CreatedAt).TotalMilliseconds;
        }

        // A private GM-suggest answer isn't part of the shared story, so it shouldn't
        // overwrite the "last GM action" the whole table sees on the admin overview.
        var game = await db.Games.FindAsync(msg.GameId);
        if (game != null && call?.RequestedByPlayerId is null)
        {
            var preview = msg.NarrativeText.Length > 80 ? msg.NarrativeText[..80] + "…" : msg.NarrativeText;
            game.LastGMAction = preview;
            game.LastGMActionAt = DateTimeOffset.UtcNow;
        }

        Message? saved = null;
        if (msg.SessionId != Guid.Empty)
        {
            saved = new Message
            {
                SessionId = msg.SessionId,
                Content = msg.NarrativeText,
                Type = "GM",
                // Set only for a private TriggerSuggest ask — makes MessagesController's
                // whisper filter hide this reply from every player except the one who asked.
                WhisperToId = call?.RequestedByPlayerId,
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.Messages.Add(saved);
        }

        await CleanUpCoordinatorAsync(db);
        await db.SaveChangesAsync();
        await activity.BroadcastAsync(msg.GameId, SagaStep.Completed);
        await context.PublishAsync(new GameNarrationStarted(msg.GameId, msg.AgentCallId));

        if (saved != null)
        {
            var dto = new MessageDto(saved.Id, saved.SessionId, null, saved.Content, saved.Type, false, saved.CreatedAt, null);

            // A private ask (TriggerSuggest) must reach only the player who asked — this used
            // to always broadcast to the whole game group regardless of Action, so every
            // player saw every GM-suggest reply, including the empty ones.
            if (call?.RequestedByPlayerId is { } requesterId)
            {
                var requester = await db.Players.FindAsync(requesterId);
                if (requester != null)
                    await hub.Clients.User(requester.UserId.ToString()).SendAsync("NewMessage", dto);
            }
            else
            {
                await hub.Clients.Group(msg.GameId.ToString()).SendAsync("NewMessage", dto);
            }
        }

        MarkCompleted();
    }

    // Retries exhausted — terminal. AgentCallFailed is deliberately NOT handled here;
    // AgentCallFailedHandler owns the retry decision and publishes this when it gives up.
    public async Task Handle(AgentCallAbandoned msg, AppDbContext db, IGmActivityBroadcaster activity, IHubContext<GameHub> hub)
    {
        CurrentState = "Failed";

        var call = await db.AgentCalls.FindAsync(msg.AgentCallId);
        if (call != null)
        {
            call.Status = AgentCallStatus.Failed;
            call.Error = msg.Error;
            call.AdvanceStep(SagaStep.Failed);
            call.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await CleanUpCoordinatorAsync(db);
        await db.SaveChangesAsync();
        await activity.BroadcastAsync(msg.GameId, SagaStep.Failed, msg.Error);

        // The client clears its activity chip on step "Failed" but never surfaces msg.Error
        // from that event — GameChatPage has had a "GMError" listener wired up since the
        // turn-observability work, but nothing on the server ever sent one. Without this, a
        // GM turn that exhausts its retries fails completely silently: the chip just goes
        // back to "GM Active" as if nothing happened, no message, no error, nothing.
        await hub.Clients.Group(msg.GameId.ToString()).SendAsync("GMError", new { message = msg.Error });

        MarkCompleted();
    }

    // Timeout — fail stuck sagas. Wolverine still delivers the scheduled message after the
    // saga completes, so this must tolerate being called on an already-finished saga.
    public async Task Handle(SagaTimeout msg, AppDbContext db, IGmActivityBroadcaster activity)
    {
        if (CurrentState is "Completed" or "Failed") return;

        CurrentState = "Failed";
        var call = await db.AgentCalls.FindAsync(AgentCallId);
        if (call != null && call.Status == AgentCallStatus.Running)
        {
            call.Status = AgentCallStatus.Failed;
            call.Error = "Saga timed out after 5 minutes";
            call.AdvanceStep(SagaStep.Failed);
            call.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await CleanUpCoordinatorAsync(db);
        await db.SaveChangesAsync();
        await activity.BroadcastAsync(GameId, SagaStep.Failed, "Saga timed out after 5 minutes");
        MarkCompleted();
    }

    /// <summary>Coordinator rows are per-agent-call scratch state and were never cleaned up.</summary>
    private async Task CleanUpCoordinatorAsync(AppDbContext db)
    {
        var coordinators = await db.ToolCallCoordinators
            .Where(c => c.AgentCallId == AgentCallId)
            .ToListAsync();

        if (coordinators.Count > 0)
            db.ToolCallCoordinators.RemoveRange(coordinators);
    }
}
