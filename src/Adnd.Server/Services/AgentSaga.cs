using Adnd.Server.Data;
using Adnd.Server.Events;
using Adnd.Server.Models;
using Microsoft.EntityFrameworkCore;
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
        ToolsRemaining = Math.Max(0, ToolsRemaining - 1);
        if (ToolsRemaining == 0)
            CurrentState = "LLMFollowUp";
    }

    // Narrative ready — saga is complete
    public async Task Handle(NarrativeReady msg, AppDbContext db)
    {
        CurrentState = "Completed";
        await CleanUpCoordinatorAsync(db);
        MarkCompleted();
    }

    // Retries exhausted — terminal. AgentCallFailed is deliberately NOT handled here;
    // AgentCallFailedHandler owns the retry decision and publishes this when it gives up.
    public async Task Handle(AgentCallAbandoned msg, AppDbContext db)
    {
        CurrentState = "Failed";

        var call = await db.AgentCalls.FindAsync(msg.AgentCallId);
        if (call != null)
        {
            call.Status = AgentCallStatus.Failed;
            call.Error = msg.Error;
            call.CurrentStep = (int)SagaStep.Failed;
            call.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await CleanUpCoordinatorAsync(db);
        await db.SaveChangesAsync();
        MarkCompleted();
    }

    // Timeout — fail stuck sagas. Wolverine still delivers the scheduled message after the
    // saga completes, so this must tolerate being called on an already-finished saga.
    public async Task Handle(SagaTimeout msg, AppDbContext db)
    {
        if (CurrentState is "Completed" or "Failed") return;

        CurrentState = "Failed";
        var call = await db.AgentCalls.FindAsync(AgentCallId);
        if (call != null && call.Status == AgentCallStatus.Running)
        {
            call.Status = AgentCallStatus.Failed;
            call.Error = "Saga timed out after 5 minutes";
            call.CurrentStep = (int)SagaStep.Failed;
            call.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await CleanUpCoordinatorAsync(db);
        await db.SaveChangesAsync();
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
