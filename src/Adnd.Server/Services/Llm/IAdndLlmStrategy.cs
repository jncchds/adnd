using Adnd.Server.Models;

namespace Adnd.Server.Services.Llm;

public interface IAdndLlmStrategy
{
    string ProviderKey { get; }

    Task<string> CompleteAsync(LlmStrategyRequest request, CancellationToken ct = default);

    Task<LlmStrategyToolResult> CompleteWithToolsAsync(
        LlmStrategyRequest request,
        IEnumerable<GMToolDefinition> tools,
        CancellationToken ct = default);

    Task<float[]> GetEmbeddingAsync(string text, LlmStrategyConfig config, CancellationToken ct = default);
}
