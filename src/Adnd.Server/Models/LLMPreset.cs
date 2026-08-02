using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Adnd.Server.Models;

public class LLMPreset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ProviderType { get; set; } = "openaicompatible";
    public string BaseModel { get; set; } = string.Empty;
    public string? EndpointUrl { get; set; }
    public string? ApiKey { get; set; }

    // Populated in-process so providers can authenticate. Must never reach the client —
    // GET/POST/PUT /api/llmpresets/{id} previously returned the raw provider key.
    [NotMapped]
    [JsonIgnore]
    public string? DecryptedApiKey { get; set; }

    public float Temperature { get; set; } = 0.7f;
    public int MaxTokens { get; set; } = 2048;
    public float TopP { get; set; } = 0.9f;
    public float FrequencyPenalty { get; set; }
    public float PresencePenalty { get; set; }
    public bool Stream { get; set; }
    public int? TimeoutMs { get; set; }
    public string ReasoningEffort { get; set; } = "none";
    public string? EmbeddingModel { get; set; }
    public string? EmbeddingEndpointUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDefault { get; set; }
    // Cached: JsonDocument.Parse("{}").RootElement as a field initializer allocates a new
    // document per entity and never disposes it, pinning its pooled buffer for the
    // object's lifetime.
    private static readonly JsonElement EmptyObject = JsonSerializer.SerializeToElement(new { });

    public JsonElement ExtraParams { get; set; } = EmptyObject;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    [JsonIgnore]
    public User User { get; set; } = null!;
    [JsonIgnore]
    public ICollection<Game> Games { get; set; } = [];
}
