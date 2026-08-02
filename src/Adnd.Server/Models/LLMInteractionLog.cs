namespace Adnd.Server.Models;

public class LLMInteractionLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid? OriginGameId { get; set; }
    public string SystemPrompt { get; set; } = string.Empty;
    public string UserPrompt { get; set; } = string.Empty;
    public string Response { get; set; } = string.Empty;
    public string? Reasoning { get; set; }
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
    public long DurationMs { get; set; }
    public string PresetName { get; set; } = string.Empty;
    public string EndpointUrl { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;

    // Written at dispatch time as Pending, then flipped to Completed/Failed once the provider
    // responds — so a call that's in flight (or that crashed the handler before it could log)
    // is still visible instead of leaving a silent gap in the log list.
    public EventRecordStatus Status { get; set; } = EventRecordStatus.Pending;
    public string? ErrorMessage { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    public User User { get; set; } = null!;
}
