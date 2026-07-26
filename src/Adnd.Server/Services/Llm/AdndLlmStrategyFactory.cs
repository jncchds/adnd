namespace Adnd.Server.Services.Llm;

/// <summary>
/// Static factory that maps provider type strings to lightweight strategy instances.
/// Strategies handle only request formatting and response parsing — no HTTP execution.
/// </summary>
public static class AdndLlmStrategyFactory
{
    private static readonly Dictionary<string, IAdndLlmStrategy> _strategies =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["ollama"] = new OllamaStrategy(),
            ["openai"] = new OpenAIStrategy(),
            ["openaicompatible"] = new OpenAICompatibleStrategy(),
            ["google"] = new GoogleAIStudioStrategy(),
        };

    /// <summary>
    /// Returns the strategy for the given provider type.
    /// Falls back to <see cref="OpenAICompatibleStrategy"/> for unknown provider types.
    /// </summary>
    public static IAdndLlmStrategy GetStrategy(string providerType)
    {
        if (_strategies.TryGetValue(providerType, out var strategy))
            return strategy;

        // Fall back to OpenAI-compatible for unknown provider types
        return _strategies["openaicompatible"];
    }

    /// <summary>Returns all registered strategy provider type strings.</summary>
    public static IEnumerable<string> RegisteredProviderTypes => _strategies.Keys;
}
