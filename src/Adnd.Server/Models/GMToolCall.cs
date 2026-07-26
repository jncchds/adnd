using System.Text.Json;

namespace Adnd.Server.Models;

public enum GMToolCallStatus { Pending, Running, Completed, Failed }

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
}
