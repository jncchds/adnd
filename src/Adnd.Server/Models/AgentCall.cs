using System.Text.Json;

namespace Adnd.Server.Models;

public record AgentStepEvent(SagaStep Step, DateTimeOffset At);

public class AgentCall
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GameId { get; set; }
    public Guid? SessionId { get; set; }
    public AgentType FromAgent { get; set; }
    public AgentType ToAgent { get; set; }
    public AgentAction Action { get; set; }

    // Set only for calls a single player asked for privately (TriggerSuggest). When present,
    // AgentSaga delivers the final narrative to just this player instead of broadcasting it
    // to the whole game group — previously every GM-suggest reply, including an empty one,
    // was visible to every player in the game with no way to keep it private.
    public Guid? RequestedByPlayerId { get; set; }
    public string? Input { get; set; }
    public string? Output { get; set; }
    public string? OutputMessage { get; set; }
    public AgentCallStatus Status { get; set; } = AgentCallStatus.Pending;
    public string? Error { get; set; }
    public Guid? ParentCallId { get; set; }
    public int CurrentStep { get; set; } = (int)SagaStep.None;
    public List<AgentStepEvent> StepHistory { get; set; } = [];
    public JsonElement Metadata { get; set; }
    public long? DurationMs { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public AgentCall? ParentCall { get; set; }
    public ICollection<AgentCall> ChildCalls { get; set; } = [];

    // Wolverine redelivers a handler's message on transient failure, and each redelivery
    // re-runs AdvanceStep for the same step before reaching the work that actually failed —
    // collapsing consecutive duplicates keeps retries from showing up as fake progress.
    public void AdvanceStep(SagaStep step)
    {
        CurrentStep = (int)step;
        if (StepHistory.Count == 0 || StepHistory[^1].Step != step)
            StepHistory.Add(new AgentStepEvent(step, DateTimeOffset.UtcNow));
    }
}
