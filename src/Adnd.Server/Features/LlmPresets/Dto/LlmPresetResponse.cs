namespace Adnd.Server.Features.LlmPresets.Dto;

public class LlmPresetResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string BaseModel { get; set; } = string.Empty;
    public string EmbeddingModel { get; set; } = string.Empty;
    public string SystemPrompt { get; set; } = string.Empty;
    public float Temperature { get; set; }
    public float? MaxTokens { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
