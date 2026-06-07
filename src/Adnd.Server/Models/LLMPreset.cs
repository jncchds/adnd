using System.Text.Json;

namespace Adnd.Server.Models;

/// <summary>
/// User-defined LLM provider preset. Each preset stores the configuration
/// for a specific provider (Ollama, LM Studio, OpenAI, Google AI Studio)
/// including base model, embedding model, and provider-specific settings.
/// </summary>
public class LLMPreset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ProviderType { get; set; } = string.Empty; // "ollama", "lmstudio", "openai", "google"

    // Completion settings
    public string BaseModel { get; set; } = string.Empty;
    public string? EndpointUrl { get; set; } // API base URL
    public string? ApiKey { get; set; } // Stored encrypted in production
    public float Temperature { get; set; } = 0.7f;
    public int MaxTokens { get; set; } = 2048;
    public float TopP { get; set; } = 0.9f;
    public float? FrequencyPenalty { get; set; }
    public float? PresencePenalty { get; set; }
    public bool Stream { get; set; } = false;

    // Embedding settings
    public string? EmbeddingModel { get; set; }
    public string? EmbeddingEndpointUrl { get; set; }

    // Metadata
    public bool IsActive { get; set; } = true;
    public bool IsDefault { get; set; } = false;
    public JsonElement? ExtraParams { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public User? User { get; set; }
}

public enum LLMProviderType
{
    Ollama = 0,
    LmStudio = 1,
    OpenAI = 2,
    Google = 3
}
