namespace Adnd.Server.Dtos;

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
    string? EmbeddingEndpointUrl);

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
    string? EmbeddingEndpointUrl);
