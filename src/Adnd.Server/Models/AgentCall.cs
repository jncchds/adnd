using System.Text.Json;

namespace Adnd.Server.Models;

public class AgentCall
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GameId { get; set; }
    public Guid? SessionId { get; set; }
    public AgentType FromAgent { get; set; }
    public AgentType ToAgent { get; set; }
    public AgentAction Action { get; set; }
    public string? Input { get; set; }
    public string? Output { get; set; }
    public string? OutputMessage { get; set; }
    public AgentCallStatus Status { get; set; } = AgentCallStatus.Pending;
    public string? Error { get; set; }
    public Guid? ParentCallId { get; set; }
    public int CurrentStep { get; set; } = (int)SagaStep.None;
    public JsonElement Metadata { get; set; }
    public long? DurationMs { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public AgentCall? ParentCall { get; set; }
    public ICollection<AgentCall> ChildCalls { get; set; } = [];
}
