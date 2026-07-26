using Adnd.Server.Data;
using Adnd.Server.Events;
using Adnd.Server.Models;
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

    // Start() — triggered by AgentCallQueued
    public async Task Start(AgentCallQueued msg, IMessageContext context)
    {
        Id = msg.AgentCallId;
        AgentCallId = msg.AgentCallId;
        GameId = msg.GameId;
        CurrentState = "Init";

        // Schedule a 5-minute timeout
        await context.ScheduleAsync(new SagaTimeout(Id), TimeSpan.FromMinutes(5));
    }

    // Transition on LLM response — move to tool execution or await narrative
    public void Handle(LLMResponseReceived msg)
    {
        if (msg.AgentCallId != AgentCallId) return;

        if (msg.HasToolCalls)
        {
            ToolsRemaining = msg.ToolCount;
            CurrentState = "ToolExecution";
        }
        else
        {
            CurrentState = "AwaitingNarrative";
        }
    }

    // Count down remaining tool calls
    public void Handle(ToolCallCompleted msg)
    {
        if (msg.AgentCallId != AgentCallId) return;

        ToolsRemaining = Math.Max(0, ToolsRemaining - 1);
        if (ToolsRemaining == 0)
            CurrentState = "LLMFollowUp";
    }

    // Narrative ready — saga is complete
    public void Handle(NarrativeReady msg)
    {
        if (msg.AgentCallId != AgentCallId) return;

        CurrentState = "Completed";
        MarkCompleted();
    }

    // Agent call failed — mark saga done
    public async Task Handle(AgentCallFailed msg, AppDbContext db)
    {
        if (msg.AgentCallId != AgentCallId) return;

        CurrentState = "Failed";
        var call = await db.AgentCalls.FindAsync(msg.AgentCallId);
        if (call != null)
        {
            call.Status = AgentCallStatus.Failed;
            call.Error = msg.Error;
            call.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        }

        MarkCompleted();
    }

    // Timeout — fail stuck sagas
    public async Task Handle(SagaTimeout msg, AppDbContext db)
    {
        if (CurrentState is "Completed" or "Failed") return;

        CurrentState = "Failed";
        var call = await db.AgentCalls.FindAsync(AgentCallId);
        if (call != null && call.Status == AgentCallStatus.Running)
        {
            call.Status = AgentCallStatus.Failed;
            call.Error = "Saga timed out after 5 minutes";
            call.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        }

        MarkCompleted();
    }
}
