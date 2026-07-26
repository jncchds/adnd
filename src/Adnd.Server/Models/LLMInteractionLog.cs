namespace Adnd.Server.Models;

public class LLMInteractionLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid? OriginGameId { get; set; }
    public string SystemPrompt { get; set; } = string.Empty;
    public string UserPrompt { get; set; } = string.Empty;
    public string Response { get; set; } = string.Empty;
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
    public long DurationMs { get; set; }
    public string PresetName { get; set; } = string.Empty;
    public string EndpointUrl { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;

    public User User { get; set; } = null!;
}
