using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Adnd.Server.Models;

namespace Adnd.Server.Services;

/// <summary>
/// Pluggable LLM provider interface for game master assistance.
/// Supports multiple backends (OpenAI, Anthropic, Ollama, local models).
/// </summary>
public interface ILLMProvider
{
    /// <summary>
    /// Get the provider identifier (e.g., "openai", "anthropic", "ollama").
    /// </summary>
    string ProviderId { get; }

    /// <summary>
    /// Get the API endpoint URL used by this provider.
    /// </summary>
    string EndpointUrl { get; }

    /// <summary>
    /// Extract token usage from a raw response string (JSON-parsed from the provider).
    /// Returns null if the response is not valid JSON or contains no usage data.
    /// </summary>
    (int promptTokens, int completionTokens, int totalTokens)? GetTokenUsage(string responseText);

    /// <summary>
    /// Generate text completion with optional system prompt.
    /// </summary>
    Task<string> CompleteAsync(string systemPrompt, string userPrompt, LLMOptions? options = null);

    /// <summary>
    /// Generate text in a structured format (JSON).
    /// </summary>
    Task<T> CompleteStructuredAsync<T>(string systemPrompt, string userPrompt, LLMOptions? options = null);

    /// <summary>
    /// Generate text completion with tool/function calling support.
    /// Returns the full response including tool calls if the model wants to use them.
    /// </summary>
    Task<LLMCompletionResult> CompleteWithToolsAsync(
        string systemPrompt, string userPrompt, IEnumerable<GMToolDefinition> tools, LLMOptions? options = null);

    /// <summary>
    /// Generate an embedding vector for the given text.
    /// </summary>
    Task<float[]> GetEmbeddingAsync(string text);

    /// <summary>
    /// Check if the provider is available (connection test).
    /// </summary>
    Task<bool> IsAvailableAsync();

    /// <summary>
    /// Get provider status information.
    /// </summary>
    Task<ProviderStatus> GetStatusAsync();
}

public class LLMOptions
{
    public string? Model { get; set; }
    public float Temperature { get; set; } = 0.7f;
    public int MaxTokens { get; set; } = 2048;
    public float TopP { get; set; } = 0.9f;
    public float? FrequencyPenalty { get; set; }
    public float? PresencePenalty { get; set; }
    public Dictionary<string, object>? ExtraParams { get; set; }
}

public class ProviderStatus
{
    public string ProviderId { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Result from a tool-calling LLM completion.
/// Contains the response text and any tool calls the model requested.
/// </summary>
public class LLMCompletionResult
{
    public string Content { get; set; } = string.Empty;
    public List<ToolCall> ToolCalls { get; set; } = new();
    public (int promptTokens, int completionTokens, int totalTokens)? TokenUsage { get; set; }

    public bool HasToolCalls => ToolCalls.Any();
}

/// <summary>
/// A tool call requested by the LLM.
/// </summary>
public class ToolCall
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;

    /// <summary>
    /// Deserialize the arguments to a specific type.
    /// </summary>
    public T? DeserializeArguments<T>() where T : new()
    {
        if (string.IsNullOrEmpty(Arguments)) return default;
        return JsonSerializer.Deserialize<T>(Arguments) ?? new T();
    }
}

/// <summary>
/// Registry of available LLM providers.
/// </summary>
public interface ILLMProviderRegistry
{
    ILLMProvider? GetProvider(string providerId);
    IEnumerable<ProviderStatus> GetAllStatusAsync();
    void RegisterProvider(string providerId, ILLMProvider provider);
    void ConfigureProvider(string providerId, IConfigurationSection config);
}

public class LLMProviderRegistry : ILLMProviderRegistry
{
    private readonly Dictionary<string, ILLMProvider> _providers = new();
    private readonly Dictionary<string, IConfigurationSection> _configs = new();
    private readonly ILogger<LLMProviderRegistry> _logger;

    public LLMProviderRegistry(ILogger<LLMProviderRegistry> logger)
    {
        _logger = logger;
    }

    public ILLMProvider? GetProvider(string providerId)
    {
        return _providers.TryGetValue(providerId, out var provider) ? provider : null;
    }

    public IEnumerable<ProviderStatus> GetAllStatusAsync()
    {
        return _providers.Select(p => new ProviderStatus
        {
            ProviderId = p.Key,
            Model = p.Value.ProviderId,
            IsAvailable = false,
            CheckedAt = DateTime.UtcNow
        }).ToList();
    }

    public void RegisterProvider(string providerId, ILLMProvider provider)
    {
        if (_providers.ContainsKey(providerId))
        {
            _logger.LogWarning("Overwriting LLM provider '{ProviderId}'", providerId);
        }
        _providers[providerId] = provider;
        _logger.LogInformation("Registered LLM provider: {ProviderId}", providerId);
    }

    public void ConfigureProvider(string providerId, IConfigurationSection config)
    {
        _configs[providerId] = config;
    }
}

/// <summary>
/// Base class for LLM providers with common functionality.
/// Concrete providers: OllamaLLMProvider, LmStudioLLMProvider, OpenAILLMProvider, GoogleAIStudioLLMProvider.
/// Preset-based wrappers: ProviderFromPresets.cs
/// </summary>
public abstract class BaseLLMProvider : ILLMProvider
{
    protected readonly ILogger<BaseLLMProvider> _logger;
    protected readonly IConfiguration _configuration;

    protected BaseLLMProvider(ILogger<BaseLLMProvider> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public abstract string ProviderId { get; }

    public abstract string EndpointUrl { get; }

    public abstract (int promptTokens, int completionTokens, int totalTokens)? GetTokenUsage(string responseText);

    public abstract Task<string> CompleteAsync(string systemPrompt, string userPrompt, LLMOptions? options = null);

    /// <summary>
    /// Calls CompleteAsync with a JSON-enforcing system prompt,
    /// strips markdown code fences if present, and deserializes the result.
    /// </summary>
    public async Task<T> CompleteStructuredAsync<T>(string systemPrompt, string userPrompt, LLMOptions? options = null)
    {
        var jsonPrompt = systemPrompt + "\n\nRespond with ONLY valid JSON. No markdown, no explanation.";
        var text = await CompleteAsync(jsonPrompt, userPrompt, options);

        var jsonText = StripMarkdownCodeFences(text.Trim());
        return JsonSerializer.Deserialize<T>(jsonText)
            ?? throw new InvalidOperationException("Failed to deserialize LLM response");
    }

    /// <summary>
    /// Strips surrounding markdown code fences (```json ... ``` or ``` ... ```).
    /// </summary>
    protected static string StripMarkdownCodeFences(string text)
    {
        if (text.StartsWith("```"))
        {
            var lines = text.Split('\n');
            return string.Join('\n', lines.Skip(1).Take(lines.Length - 2));
        }
        return text;
    }

    public abstract Task<float[]> GetEmbeddingAsync(string text);

    public abstract Task<bool> IsAvailableAsync();

    public abstract Task<ProviderStatus> GetStatusAsync();

    /// <summary>
    /// Default tool-calling implementation that falls back to plain text completion.
    /// Override in concrete providers that support native tool calling (OpenAI, Ollama with tools, Google AI).
    /// </summary>
    public virtual async Task<LLMCompletionResult> CompleteWithToolsAsync(
        string systemPrompt, string userPrompt, IEnumerable<GMToolDefinition> tools, LLMOptions? options = null)
    {
        // Fallback: append tool descriptions to system prompt and ask for JSON output
        var toolDefs = tools.ToList();
        if (!toolDefs.Any())
        {
            var fallbackResult = await CompleteAsync(systemPrompt, userPrompt, options);
            return new LLMCompletionResult { Content = fallbackResult, TokenUsage = GetTokenUsage(fallbackResult) };
        }

        var toolDescriptions = string.Join("\n\n", toolDefs.Select(t =>
            $"Tool: {t.Name}\nDescription: {t.Description}\nParameters schema: {JsonSerializer.Serialize(t.Parameters)}"));

        var enhancedSystemPrompt = $"{systemPrompt}\n\n# Available Tools\n{toolDescriptions}" +
            "\n\nIf you need to use a tool, respond with a JSON array of tool calls:\n" +
            "[{\"id\": \"call_1\", \"name\": \"tool_name\", \"arguments\": {}}]\n" +
            "Otherwise respond with your narrative text.";

        var result = await CompleteAsync(enhancedSystemPrompt, userPrompt, options);

        // Try to parse tool calls from the response
        var parsed = ParseToolCallsFromResponse(result);

        return new LLMCompletionResult
        {
            Content = result,
            ToolCalls = parsed,
            TokenUsage = GetTokenUsage(result)
        };
    }

    /// <summary>
    /// Parse tool calls from the LLM response text.
    /// Looks for JSON arrays of {id, name, arguments} objects.
    /// </summary>
    protected List<ToolCall> ParseToolCallsFromResponse(string response)
    {
        // Try to find a JSON array in the response
        var trimmed = response.Trim();
        if (!trimmed.StartsWith("[")) return new List<ToolCall>();

        try
        {
            // Find the first complete JSON array
            var bracketCount = 0;
            var arrayEnd = -1;
            for (var i = 0; i < trimmed.Length; i++)
            {
                if (trimmed[i] == '[') bracketCount++;
                else if (trimmed[i] == ']')
                {
                    bracketCount--;
                    if (bracketCount == 0)
                    {
                        arrayEnd = i + 1;
                        break;
                    }
                }
            }

            if (arrayEnd < 0) return new List<ToolCall>();

            var json = trimmed[..arrayEnd];
            var calls = JsonSerializer.Deserialize<List<ToolCallRequest>>(json);
            if (calls == null || !calls.Any()) return new List<ToolCall>();

            return calls.Select(c => new ToolCall
            {
                Id = c.id ?? $"call_{Guid.NewGuid():N[..8]}",
                Name = c.name,
                Arguments = c.arguments
            }).ToList();
        }
        catch
        {
            return new List<ToolCall>();
        }
    }

    private class ToolCallRequest
    {
        public string? id { get; set; }
        public string? name { get; set; }
        public string? arguments { get; set; }
    }
}
