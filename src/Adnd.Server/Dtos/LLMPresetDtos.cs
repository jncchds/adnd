using Adnd.Server.Models;

namespace Adnd.Server.Dtos;

public record QueryModelsDto(string ProviderType, string? EndpointUrl, string? ApiKey);

/// <summary>
/// Response shape for presets. Exists so the API cannot leak the provider key: the entity
/// carries both the encrypted ApiKey and the in-process DecryptedApiKey, and scrubbing
/// those by hand on a tracked entity was both error-prone and easy to forget.
/// </summary>
public record LLMPresetDto(
    Guid Id,
    string Name,
    string ProviderType,
    string BaseModel,
    string? EndpointUrl,
    bool HasApiKey,
    float Temperature,
    int MaxTokens,
    float TopP,
    float FrequencyPenalty,
    float PresencePenalty,
    bool Stream,
    int? TimeoutMs,
    string ReasoningEffort,
    string? EmbeddingModel,
    string? EmbeddingEndpointUrl,
    bool IsActive,
    bool IsDefault,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static LLMPresetDto From(LLMPreset p) => new(
        p.Id, p.Name, p.ProviderType, p.BaseModel, p.EndpointUrl,
        !string.IsNullOrEmpty(p.ApiKey),
        p.Temperature, p.MaxTokens, p.TopP, p.FrequencyPenalty, p.PresencePenalty,
        p.Stream, p.TimeoutMs, p.ReasoningEffort, p.EmbeddingModel, p.EmbeddingEndpointUrl,
        p.IsActive, p.IsDefault, p.CreatedAt, p.UpdatedAt);
}

public record CreateLLMPresetDto(
    string Name,
    string ProviderType,
    string BaseModel,
    string? EndpointUrl,
    string? ApiKey,
    float Temperature,
    int MaxTokens,
    float TopP,
    float FrequencyPenalty,
    float PresencePenalty,
    bool Stream,
    int? TimeoutMs,
    string ReasoningEffort,
    string? EmbeddingModel,
    string? EmbeddingEndpointUrl,
    bool IsDefault = false);

public record UpdateLLMPresetDto(
    string Name,
    string? ProviderType,
    string? BaseModel,
    string? EndpointUrl,
    string? ApiKey,
    float? Temperature,
    int? MaxTokens,
    float? TopP,
    float? FrequencyPenalty,
    float? PresencePenalty,
    bool? Stream,
    int? TimeoutMs,
    string? ReasoningEffort,
    string? EmbeddingModel,
    string? EmbeddingEndpointUrl,
    bool? IsDefault = null);
