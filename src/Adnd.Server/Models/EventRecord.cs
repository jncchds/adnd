using System.Text.Json;

namespace Adnd.Server.Models;

public class EventRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GameId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public JsonElement Payload { get; set; }
    public Guid CorrelationId { get; set; } = Guid.NewGuid();
    public EventRecordStatus Status { get; set; } = EventRecordStatus.Pending;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ProcessedAt { get; set; }
}
