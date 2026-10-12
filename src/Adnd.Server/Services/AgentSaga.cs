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

/// <summary>Timeout message for AgentSaga — Id matches saga Id for Wolverine correlation.
/// The clock restarts with every LLM response, so only the timeout scheduled for the round
/// the saga is still in may fail it.</summary>
public record SagaTimeout(Guid Id, int Round = 0);

/// <summary>
/// Wolverine durable saga keyed on AgentCall.Id. One saga is one GM turn, and a turn is a
/// loop: the model answers, its tools run, it is shown the results and answers again, until
/// it replies with narration and no tools. A turn used to be a single round of tools chosen
/// blind followed by a tool-less narration call, so the GM could never act on a roll it had
/// just made — it applied damage before knowing an attack hit, and could not play an enemy's
/// turn at all. A 5-minute timeout per round fails stuck sagas.
/// </summary>
public class AgentSaga : Wolverine.Saga
{
    /// <summary>LLM responses per turn. The last one is made without tools, so the turn
    /// always ends in narration.</summary>
    public const int MaxRounds = 60;

    /// <summary>How often one turn may be sent back because the fight is still waiting on the GM.</summary>
    private const int MaxContinuations = 20;

    public Guid Id { get; set; }
    public Guid AgentCallId { get; set; }
    public Guid GameId { get; set; }
    public int ToolsRemaining { get; set; }
    public Guid? CurrentToolId { get; set; }
    public string CurrentState { get; set; } = "Init";
    public int Round { get; set; }
    public int Continuations { get; set; }

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

        await context.ScheduleAsync(new SagaTimeout(Id, Round), TimeSpan.FromMinutes(5));

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

    // One LLM response: run its tools and come back, or end the turn. Wolverine routes
    // LLMResponseReceived (it carries [SagaIdentity]) solely to this Handle method — a
    // separate LLMResponseHandler class used to sit alongside it and was silently never
    // invoked, so the orchestration lives here.
    public async Task Handle(LLMResponseReceived msg, IMessageContext context, AppDbContext db, ISessionManagementService sessions,
        IGmActivityBroadcaster activity, ICombatTracker combats, IHubContext<GameHub> hub)
    {
        var call = await db.AgentCalls.FindAsync(msg.AgentCallId);
        if (call == null) return;

        Round++;
        await context.ScheduleAsync(new SagaTimeout(Id, Round), TimeSpan.FromMinutes(5));

        call.Output = msg.ResponseText;
        var text = msg.ResponseText?.Trim() ?? "";

        List<ToolCall> toolCalls = [];
        if (msg.HasToolCalls && !string.IsNullOrEmpty(msg.RawJson))
            toolCalls = JsonSerializer.Deserialize<List<ToolCall>>(msg.RawJson) ?? [];

        // Sent back for more, a model often repeats the line it has just said. Said twice it
        // reads as a stutter, so a narrate call the table has already had is dropped.
        if (toolCalls.Any(t => t.Name == "narrate"))
        {
            var heard = TurnLog.Read((await db.ToolCallCoordinators.AsNoTracking()
                .FirstOrDefaultAsync(c => c.AgentCallId == msg.AgentCallId))?.ToolResults);
            toolCalls.RemoveAll(t => t.Name == "narrate"
                && t.Arguments.ValueKind == JsonValueKind.Object
                && t.Arguments.TryGetProperty("text", out var said) && said.ValueKind == JsonValueKind.String
                && TurnLog.AlreadySaid(heard, said.GetString()!));
        }

        // The GM deciding that nothing needs to happen is a legitimate outcome, not a failure:
        // players talking among themselves shouldn't force the world to react to every line.
        // Restricted to Narrate because the other actions are explicit GM/admin requests for
        // output, where silence would just look broken.
        var waitOnly = call.Action == AgentAction.Narrate && toolCalls.Count > 0 && toolCalls.All(t => t.Name == "wait");

        if (toolCalls.Count == 0 || waitOnly)
        {
            if (await FightWaitsOnGmAsync(call, combats) is { } directive)
            {
                var pending = await GetOrAddCoordinatorAsync(db);
                var soFar = TurnLog.Read(pending.ToolResults);
                if (!waitOnly && text.Length > 0 && !TurnLog.AlreadySaid(soFar, text))
                {
                    var session = await sessions.GetOrCreateCurrentSessionAsync(msg.GameId);
                    await PostNarrationAsync(db, hub, session.Id, call, text);
                    soFar.Add(new TurnLogEntry(Round, TurnLog.Said, "", null, text));
                }
                // Words alone are the usual way a turn stalls here: the model announces the
                // attack and stops, waiting to be asked again.
                soFar.Add(new TurnLogEntry(Round, TurnLog.Directive, "", null,
                    waitOnly ? directive : $"You replied with words only, and words do not move the fight. {directive}"));
                pending.ToolResults = TurnLog.Write(soFar);
                await db.SaveChangesAsync();
                await RedispatchAsync(context, call);
                return;
            }

            if (waitOnly)
            {
                // No Message row is written and nothing is broadcast except the Completed
                // step that clears the activity chip.
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

                await CleanUpCoordinatorAsync(db);
                await db.SaveChangesAsync();
                await activity.BroadcastAsync(msg.GameId, SagaStep.Completed);
                MarkCompleted();
                return;
            }

            // Tools ran and the model then returned nothing at all. Retrying that as an
            // ordinary step asks it to "continue", and it does — observed live giving a
            // player character a second attack nobody asked for. All that is missing is the
            // telling, so that is all it is asked for.
            if (text.Length == 0)
            {
                var unspoken = await db.ToolCallCoordinators.FirstOrDefaultAsync(c => c.AgentCallId == msg.AgentCallId);
                var done = TurnLog.Read(unspoken?.ToolResults);
                if (unspoken != null && done.Count > 0 && !TurnLog.HasSpoken(done) && done[^1].Kind != TurnLog.Close)
                {
                    done.Add(new TurnLogEntry(Round, TurnLog.Close, "", null,
                        "You ended your turn without telling the table anything. Everything above has happened; tell them now what it looked like and where things stand."));
                    unspoken.ToolResults = TurnLog.Write(done);
                    await db.SaveChangesAsync();
                    await RedispatchAsync(context, call);
                    return;
                }
            }

            CurrentState = "AwaitingNarrative";
            call.AdvanceStep(SagaStep.NarrativeReady);
            await db.SaveChangesAsync();
            await activity.BroadcastAsync(msg.GameId, SagaStep.NarrativeReady);

            var current = await sessions.GetOrCreateCurrentSessionAsync(msg.GameId);
            await context.PublishAsync(new NarrativeReady(msg.AgentCallId, msg.GameId, current.Id, msg.ResponseText ?? ""));
            return;
        }

        ToolsRemaining = toolCalls.Count;
        CurrentState = "ToolExecution";
        call.AdvanceStep(SagaStep.ToolExecution);
        await activity.BroadcastAsync(msg.GameId, SagaStep.ToolExecution);

        var coordinator = await GetOrAddCoordinatorAsync(db);
        var log = TurnLog.Read(coordinator.ToolResults);

        // Text that comes with tool calls is said before they run, so the log reads in the
        // order things happened: the swing, then the roll, then what the roll meant. It used
        // to be thrown away in favour of the closing narration.
        if (text.Length > 0 && !TurnLog.AlreadySaid(log, text))
        {
            var session = await sessions.GetOrCreateCurrentSessionAsync(msg.GameId);
            await PostNarrationAsync(db, hub, session.Id, call, text);
            log.Add(new TurnLogEntry(Round, TurnLog.Said, "", null, text));
        }

        coordinator.ToolResults = TurnLog.Write(log);
        coordinator.ToolCalls = JsonSerializer.SerializeToElement(toolCalls);
        coordinator.TotalTools = toolCalls.Count;
        coordinator.CompletedTools = 0;
        coordinator.CurrentToolIndex = 0;
        await db.SaveChangesAsync();

        var first = toolCalls[0];
        await context.PublishAsync(new ToolCallRequested(msg.AgentCallId, msg.GameId, first.Name, first.Arguments.GetRawText(), 0));
    }

    // Record a tool's result, run the next tool of this round, or go back to the model with
    // everything the turn has done so far. Saga-owned message: a separate CoordinatorHandler
    // class used to sit alongside this and was never invoked.
    public async Task Handle(ToolCallCompleted msg, IMessageContext context, AppDbContext db, ISessionManagementService sessions,
        IGmActivityBroadcaster activity, ICombatTracker combats)
    {
        ToolsRemaining = Math.Max(0, ToolsRemaining - 1);

        var coordinator = await db.ToolCallCoordinators.FirstOrDefaultAsync(c => c.AgentCallId == msg.AgentCallId);
        if (coordinator == null) return;

        var toolCalls = coordinator.ToolCalls.ValueKind == JsonValueKind.Array
            ? JsonSerializer.Deserialize<List<ToolCall>>(coordinator.ToolCalls.GetRawText()) ?? []
            : [];

        var arguments = coordinator.CurrentToolIndex < toolCalls.Count
            ? toolCalls[coordinator.CurrentToolIndex].Arguments.GetRawText()
            : null;
        var log = TurnLog.Read(coordinator.ToolResults);
        log.Add(new TurnLogEntry(Round, TurnLog.Tool, msg.ToolName, arguments, msg.ResultJson));
        coordinator.ToolResults = TurnLog.Write(log);
        coordinator.CompletedTools++;
        coordinator.CurrentToolIndex++;
        await db.SaveChangesAsync();

        var call = await db.AgentCalls.FindAsync(msg.AgentCallId);
        if (call == null) return;

        if (coordinator.CurrentToolIndex < toolCalls.Count)
        {
            var next = toolCalls[coordinator.CurrentToolIndex];
            await context.PublishAsync(new ToolCallRequested(msg.AgentCallId, msg.GameId, next.Name, next.Arguments.GetRawText(), coordinator.CurrentToolIndex));
            return;
        }

        // One round that only spoke is a model describing the attempt before it reaches for
        // the dice — observed live: it narrated a charge, and ending the turn there meant no
        // fight ever started. Two in a row is a model narrating through the tool a round at a
        // time, and that is the end of the turn.
        var previous = log.Where(e => e.Round == Round - 1).ToList();
        if (toolCalls.All(t => t.Name is "narrate" or "wait")
            && previous.Count > 0 && previous.All(e => e.Kind == TurnLog.Said || e.Name is "narrate" or "wait"))
        {
            if (await FightWaitsOnGmAsync(call, combats) is { } directive)
            {
                log.Add(new TurnLogEntry(Round, TurnLog.Directive, "", null, directive));
                coordinator.ToolResults = TurnLog.Write(log);
                await db.SaveChangesAsync();
                await RedispatchAsync(context, call);
                return;
            }

            CurrentState = "AwaitingNarrative";
            call.AdvanceStep(SagaStep.NarrativeReady);
            await db.SaveChangesAsync();
            await activity.BroadcastAsync(msg.GameId, SagaStep.NarrativeReady);
            var session = await sessions.GetOrCreateCurrentSessionAsync(msg.GameId);
            await context.PublishAsync(new NarrativeReady(msg.AgentCallId, msg.GameId, session.Id, ""));
            return;
        }

        await RedispatchAsync(context, call);
    }

    // Narrative ready — save + broadcast the message and complete the saga. Saga-owned
    // message: the standalone NarrativeHandler class that used to do this was silently never
    // invoked, so every turn reached this point and vanished.
    public async Task Handle(NarrativeReady msg, IMessageContext context, AppDbContext db, IHubContext<GameHub> hub, IGmActivityBroadcaster activity)
    {
        var call = await db.AgentCalls.FindAsync(msg.AgentCallId);
        var blank = string.IsNullOrWhiteSpace(msg.NarrativeText);

        // The LLM call can "succeed" (no exception, normal HTTP 200) yet still produce zero
        // narrative text — observed live with a reasoning model that spent its whole
        // completion budget on internal chain-of-thought and never wrote an actual answer.
        // Saving that as a Message produced a blank "Game Master" chat bubble. It is a
        // failure to retry — unless the GM already spoke earlier in this turn, in which case
        // having nothing to add is simply the end of the turn.
        if (blank)
        {
            var coordinator = await db.ToolCallCoordinators.AsNoTracking().FirstOrDefaultAsync(c => c.AgentCallId == msg.AgentCallId);
            if (!TurnLog.HasSpoken(TurnLog.Read(coordinator?.ToolResults)))
            {
                await context.PublishAsync(new AgentCallFailed(msg.AgentCallId, msg.GameId, "The GM's response came back empty."));
                return;
            }
        }

        CurrentState = "Completed";

        if (call != null)
        {
            call.Status = AgentCallStatus.Completed;
            call.AdvanceStep(SagaStep.Completed);
            call.OutputMessage = msg.NarrativeText;
            call.DurationMs = (long)(DateTimeOffset.UtcNow - call.CreatedAt).TotalMilliseconds;
        }

        await CleanUpCoordinatorAsync(db);
        await db.SaveChangesAsync();
        await activity.BroadcastAsync(msg.GameId, SagaStep.Completed);
        await context.PublishAsync(new GameNarrationStarted(msg.GameId, msg.AgentCallId));

        if (!blank && msg.SessionId != Guid.Empty)
            await PostNarrationAsync(db, hub, msg.SessionId, call, msg.NarrativeText.Trim());

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
        // from that event, so without this a GM turn that exhausts its retries fails
        // completely silently.
        await hub.Clients.Group(msg.GameId.ToString()).SendAsync("GMError", new { message = msg.Error });

        MarkCompleted();
    }

    // Timeout — fail stuck sagas. Wolverine still delivers the scheduled message after the
    // saga completes, so this must tolerate being called on an already-finished saga, and a
    // timeout scheduled for an earlier round says nothing about the current one.
    public async Task Handle(SagaTimeout msg, AppDbContext db, IGmActivityBroadcaster activity)
    {
        if (CurrentState is "Completed" or "Failed") return;
        if (msg.Round != Round) return;

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

    /// <summary>
    /// The model considers its turn over; it is only over if the next move belongs to a
    /// player. Left on an enemy's turn, the fight would sit there until someone typed, and
    /// that line would then be read as speaking out of turn. Returns what to tell the model,
    /// or null when the turn may end.
    /// </summary>
    private async Task<string?> FightWaitsOnGmAsync(AgentCall call, ICombatTracker combats)
    {
        if (call.Action != AgentAction.Narrate || Round >= MaxRounds || Continuations >= MaxContinuations) return null;
        if (await combats.PendingGmTurnAsync(GameId) is not { } directive) return null;
        Continuations++;
        return directive;
    }

    /// <summary>Back to the model. LLMDispatchHandler adds the turn so far to the prompt.</summary>
    private async Task RedispatchAsync(IMessageContext context, AgentCall call)
    {
        CurrentState = "LLMFollowUp";

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
                // Start already validated Input; a failure here is non-fatal.
            }
        }

        await context.PublishAsync(new LLMDispatchRequested(call.Id, call.GameId, systemPrompt, userPrompt));
    }

    /// <summary>Saves a line of GM narration and pushes it — to the table, or to the one
    /// player who asked privately (TriggerSuggest), whose reply nobody else may see.</summary>
    private async Task PostNarrationAsync(AppDbContext db, IHubContext<GameHub> hub, Guid sessionId, AgentCall? call, string text)
    {
        // A private answer isn't part of the shared story, so it shouldn't overwrite the
        // "last GM action" on the admin overview.
        var game = await db.Games.FindAsync(GameId);
        if (game != null && call?.RequestedByPlayerId is null)
        {
            game.LastGMAction = text.Length > 80 ? text[..80] + "…" : text;
            game.LastGMActionAt = DateTimeOffset.UtcNow;
        }

        var saved = new Message
        {
            SessionId = sessionId,
            Content = text,
            Type = "GM",
            WhisperToId = call?.RequestedByPlayerId,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Messages.Add(saved);
        await db.SaveChangesAsync();

        var dto = new MessageDto(saved.Id, saved.SessionId, null, saved.Content, saved.Type, false, saved.CreatedAt, null);
        if (call?.RequestedByPlayerId is { } requesterId)
        {
            var requester = await db.Players.FindAsync(requesterId);
            if (requester != null)
                await hub.Clients.User(requester.UserId.ToString()).SendAsync("NewMessage", dto);
        }
        else
        {
            await hub.Clients.Group(GameId.ToString()).SendAsync("NewMessage", dto);
        }
    }

    /// <summary>One row per turn: the tool calls of the current round, and the log of every round.</summary>
    private async Task<ToolCallCoordinator> GetOrAddCoordinatorAsync(AppDbContext db)
    {
        var coordinator = await db.ToolCallCoordinators.FirstOrDefaultAsync(c => c.AgentCallId == AgentCallId);
        if (coordinator != null) return coordinator;

        coordinator = new ToolCallCoordinator
        {
            AgentCallId = AgentCallId,
            ToolResults = TurnLog.Write([]),
            ToolCalls = JsonSerializer.SerializeToElement(new List<ToolCall>())
        };
        db.ToolCallCoordinators.Add(coordinator);
        return coordinator;
    }

    /// <summary>Coordinator rows are per-agent-call scratch state.</summary>
    private async Task CleanUpCoordinatorAsync(AppDbContext db)
    {
        var coordinators = await db.ToolCallCoordinators
            .Where(c => c.AgentCallId == AgentCallId)
            .ToListAsync();

        if (coordinators.Count > 0)
            db.ToolCallCoordinators.RemoveRange(coordinators);
    }
}
