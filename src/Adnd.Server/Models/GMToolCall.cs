using System.Text.Json;

namespace Adnd.Server.Models;

// New members are appended so existing persisted int values keep their meaning.
public enum GMToolCallStatus { Pending, Running, Completed, Failed, AwaitingConfirmation, Declined, AwaitingReroll }

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

    /// <summary>
    /// Set while the call is parked in <see cref="GMToolCallStatus.AwaitingReroll"/>: the
    /// roll has already happened and been shown to the table, but the turn is held open so
    /// the GM is told the final number rather than one the player is about to replace.
    /// </summary>
    public Guid? RerollCharacterId { get; set; }

    /// <summary>
    /// The chat message carrying the question this call is waiting on. The answer replaces
    /// that row in place, so the prompt becomes its own outcome rather than vanishing.
    /// </summary>
    public Guid? PromptMessageId { get; set; }
}
