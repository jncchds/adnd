namespace Adnd.Server.Models;

public class EventRecord
{
    public Guid Id { get; set; }
    public Guid GameId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public EventStatus Status { get; set; }
    public string? CorrelationId { get; set; }
    public int RetryCount { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime? AckedAt { get; set; }
    public string? Error { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
