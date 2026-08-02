using System.Text.Json;

namespace Adnd.Server.Models;

// New members are appended so existing persisted int values keep their meaning.
public enum GMToolCallStatus { Pending, Running, Completed, Failed, AwaitingConfirmation, Declined }

public class GMToolCall
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GameId { get; set; }
    public Guid SessionId { get; set; }
    public string ToolName { get; set; } = string.Empty;
    public JsonElement Arguments { get; set; }
    public JsonElement Result { get; set; }
    public GMToolCallStatus Status { get; set; } = GMToolCallStatus.Pending;
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }

    // Set when the call is gated on player confirmation, so it can be listed via
    // /api/gmtools/pending and resumed into the saga once resolved.
    public Guid? AgentCallId { get; set; }
    public int ToolIndex { get; set; }
    public bool RequiresConfirmation { get; set; }
    public Guid? TargetPlayerId { get; set; }
}
