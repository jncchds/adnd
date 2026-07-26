namespace Adnd.Server.Services.Llm;

/// <summary>
/// Registry of all LLM strategies keyed by provider type string.
/// Provider keys match LLMPreset.ProviderType values.
/// </summary>
public static class AdndLlmStrategyFactory
{
    private static readonly Dictionary<string, IAdndLlmStrategy> _strategies = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ollama"] = new OllamaStrategy(),
        ["openai"] = new OpenAIStrategy(),
        ["google"] = new GoogleAIStudioStrategy(),
        ["lmstudio"] = new OpenAICompatibleStrategy(),
    };

    public static IAdndLlmStrategy Get(string providerKey)
    {
        if (_strategies.TryGetValue(providerKey, out var strategy))
            return strategy;
        throw new InvalidOperationException($"No LLM strategy registered for provider key '{providerKey}'. " +
            $"Known keys: {string.Join(", ", _strategies.Keys)}");
    }

    public static bool TryGet(string providerKey, out IAdndLlmStrategy strategy)
        => _strategies.TryGetValue(providerKey, out strategy!);
}
