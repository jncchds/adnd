using Adnd.Server.Models;

namespace Adnd.Server.Services.Llm;

public record LlmStrategyConfig(
    string Endpoint,
    string ModelName,
    string? ApiKey,
    string? EmbeddingModel,
    string? EmbeddingEndpoint,
    int? TimeoutMs,
    string? ReasoningEffort
);

public record LlmStrategyRequest(
    LlmStrategyConfig Config,
    string SystemPrompt,
    string UserPrompt,
    float Temperature,
    int MaxTokens,
    JsonSchemaOutput? JsonSchema
);

public record LlmStrategyToolResult(
    string Content,
    List<ToolCall> ToolCalls,
    (int promptTokens, int completionTokens, int totalTokens)? TokenUsage
);
