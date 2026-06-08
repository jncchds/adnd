using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Primitives;
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

/// <summary>
/// Ollama provider - for local/running LLM instances.
/// </summary>
public class OllamaLLMProvider : BaseLLMProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _model;

    public OllamaLLMProvider(
        ILogger<OllamaLLMProvider> logger,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory)
        : base(logger, configuration)
    {
        _baseUrl = configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";
        _model = configuration["Ollama:Model"] ?? "llama3";
        _httpClient = httpClientFactory.CreateClient();
    }

    public override string ProviderId => "ollama";

    public override string EndpointUrl => _baseUrl;

    public override (int promptTokens, int completionTokens, int totalTokens)? GetTokenUsage(string responseText)
    {
        try
        {
            // Ollama doesn't return token counts in chat responses
            return null;
        }
        catch
        {
            return null;
        }
    }

    public override async Task<string> CompleteAsync(string systemPrompt, string userPrompt, LLMOptions? options = null)
    {
        string modelName = options?.Model ?? _model;
        float temperature = options?.Temperature ?? 0.7f;
        int maxTokens = options?.MaxTokens ?? 2048;

        var payload = new
        {
            model = modelName,
            system = systemPrompt,
            prompt = userPrompt,
            stream = false,
            opts = new
            {
                temperature,
                num_predict = maxTokens
            }
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                _baseUrl + "/api/chat", payload,
                new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Ollama API error: {Error}", error);
                return "Error: " + response.StatusCode;
            }

            var result = await response.Content.ReadFromJsonAsync<OllamaResponse>();
            return result?.Message?.Content ?? "No response";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ollama request failed");
            return "Error: " + ex.Message;
        }
    }

    /// <summary>
    /// Ollama-native tool calling implementation.
    /// Uses Ollama's /api/chat endpoint with the tools parameter.
    /// </summary>
    public override async Task<LLMCompletionResult> CompleteWithToolsAsync(
        string systemPrompt, string userPrompt, IEnumerable<GMToolDefinition> tools, LLMOptions? options = null)
    {
        string modelName = options?.Model ?? _model;
        float temperature = options?.Temperature > 0 ? options.Temperature : 0.7f;
        int maxTokens = options?.MaxTokens > 0 ? options.MaxTokens : 2048;

        var toolDefs = tools.ToList();
        var ollamaTools = toolDefs.Select(t => new
        {
            type = "function",
            function = new
            {
                name = t.Name,
                description = t.Description,
                parameters = t.Parameters
            }
        }).ToList();

        var payload = new
        {
            model = modelName,
            system = systemPrompt,
            prompt = userPrompt,
            stream = false,
            tools = ollamaTools.Any() ? ollamaTools : null,
            opts = new
            {
                temperature,
                num_predict = maxTokens
            }
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                _baseUrl + "/api/chat", payload,
                new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Ollama tool call API error: {Error}", error);
                return new LLMCompletionResult { Content = "Error: " + response.StatusCode };
            }

            var result = await response.Content.ReadFromJsonAsync<OllamaResponse>();
            var content = result?.Message?.Content ?? "No response";

            // Ollama tool calls come in a separate field
            var toolCalls = new List<ToolCall>();
            if (result?.Message?.ToolCalls != null)
            {
                toolCalls = result.Message.ToolCalls
                    .Select(tc => new ToolCall
                    {
                        Id = tc.Id ?? $"call_{Guid.NewGuid():N[..8]}",
                        Name = tc.Function?.Name ?? "unknown",
                        Arguments = tc.Function?.Arguments ?? "{}"
                    }).ToList();
            }

            return new LLMCompletionResult
            {
                Content = content,
                ToolCalls = toolCalls,
                TokenUsage = GetTokenUsage(content)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ollama tool call request failed");
            return new LLMCompletionResult { Content = "Error: " + ex.Message };
        }
    }

    public override async Task<float[]> GetEmbeddingAsync(string text)
    {
        string embeddingModel = "nomic-embed-text";
        var payload = new { model = embeddingModel, input = text };

        var response = await _httpClient.PostAsJsonAsync(
            _baseUrl + "/api/embed", payload);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException("Ollama embedding failed: " + response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<OllamaEmbeddingResponse>();
        return result?.Embedding ?? Array.Empty<float>();
    }

    public override async Task<bool> IsAvailableAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync(_baseUrl + "/api/tags");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public override async Task<ProviderStatus> GetStatusAsync()
    {
        return new ProviderStatus
        {
            ProviderId = ProviderId,
            Model = _model,
            IsAvailable = await IsAvailableAsync(),
            CheckedAt = DateTime.UtcNow
        };
    }
}

// ==================== LM Studio Provider ====================

/// <summary>
/// LM Studio provider - OpenAI-compatible local server.
/// LM Studio exposes an OpenAI-compatible API at a configurable endpoint.
/// </summary>
public class LmStudioLLMProvider : BaseLLMProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _model;
    private readonly string? _apiKey;

    public LmStudioLLMProvider(
        ILogger<LmStudioLLMProvider> logger,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory)
        : base(logger, configuration)
    {
        _baseUrl = configuration["LmStudio:BaseUrl"] ?? "http://localhost:1234";
        _model = configuration["LmStudio:Model"] ?? "local-model";
        _apiKey = configuration["LmStudio:ApiKey"];
        _httpClient = httpClientFactory.CreateClient();
        if (_apiKey != null)
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);
    }

    public override string ProviderId => "lmstudio";

    public override string EndpointUrl => _baseUrl;

    public override (int promptTokens, int completionTokens, int totalTokens)? GetTokenUsage(string responseText)
    {
        try
        {
            var usage = System.Text.Json.JsonSerializer.Deserialize<OpenAIUsage>(responseText);
            if (usage != null && usage.TotalTokens > 0)
            {
                return (usage.PromptTokens, usage.CompletionTokens, usage.TotalTokens);
            }
        }
        catch
        {
            // Response is not the usage object, ignore
        }
        return null;
    }

    public override async Task<string> CompleteAsync(string systemPrompt, string userPrompt, LLMOptions? options = null)
    {
        string modelName = options?.Model ?? _model;
        float temperature = options?.Temperature ?? 0.7f;
        int maxTokens = options?.MaxTokens ?? 2048;
        float topP = options?.TopP ?? 0.9f;

        var payload = new
        {
            model = modelName,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            stream = false,
            temperature,
            max_tokens = maxTokens,
            top_p = topP,
            frequency_penalty = options?.FrequencyPenalty,
            presence_penalty = options?.PresencePenalty
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                _baseUrl + "/v1/chat/completions", payload);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("LM Studio API error: {Error}", error);
                return "Error: " + response.StatusCode;
            }

            var responseResult = await response.Content.ReadFromJsonAsync<OpenAIChatResponse>();
            return responseResult?.Choices?.FirstOrDefault()?.Message?.Content ?? "No response";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LM Studio request failed");
            return "Error: " + ex.Message;
        }
    }

    /// <summary>
    /// LM Studio (OpenAI-compatible) tool calling implementation.
    /// </summary>
    public override async Task<LLMCompletionResult> CompleteWithToolsAsync(
        string systemPrompt, string userPrompt, IEnumerable<GMToolDefinition> tools, LLMOptions? options = null)
    {
        string modelName = options?.Model ?? _model;
        float temperature = options?.Temperature > 0 ? options.Temperature : 0.7f;
        int maxTokens = options?.MaxTokens > 0 ? options.MaxTokens : 2048;
        float topP = options?.TopP > 0 ? options.TopP : 0.9f;

        var toolDefs = tools.ToList();
        var toolsPayload = toolDefs.Select(t => t.ToOpenAISchema()).ToList();

        var payload = new
        {
            model = modelName,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            stream = false,
            temperature,
            max_tokens = maxTokens,
            top_p = topP,
            frequency_penalty = options?.FrequencyPenalty,
            presence_penalty = options?.PresencePenalty,
            tools = toolsPayload.Any() ? toolsPayload : null,
            tool_choice = toolsPayload.Any() ? new { type = "auto" } : null
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                _baseUrl + "/v1/chat/completions", payload);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("LM Studio tool call API error: {Error}", error);
                return new LLMCompletionResult { Content = "Error: " + response.StatusCode };
            }

            var responseText = await response.Content.ReadAsStringAsync();
            var result = await response.Content.ReadFromJsonAsync<OpenAIChatResponse>();

            var content = result?.Choices?.FirstOrDefault()?.Message?.Content ?? "No response";
            var toolCalls = result?.Choices?.FirstOrDefault()?.Message?.ToolCalls
                ?.Select(tc => new ToolCall
                {
                    Id = tc.Id ?? $"call_{Guid.NewGuid():N[..8]}",
                    Name = tc.Function?.Name ?? "unknown",
                    Arguments = tc.Function?.Arguments ?? "{}"
                }).ToList() ?? new List<ToolCall>();

            return new LLMCompletionResult
            {
                Content = content,
                ToolCalls = toolCalls,
                TokenUsage = GetTokenUsage(responseText)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LM Studio tool call request failed");
            return new LLMCompletionResult { Content = "Error: " + ex.Message };
        }
    }

    public override async Task<float[]> GetEmbeddingAsync(string text)
    {
        var payload = new { model = "nomic-embed-text", input = text };
        var response = await _httpClient.PostAsJsonAsync(
            _baseUrl + "/v1/embeddings", payload);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException("LM Studio embedding failed: " + response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<OpenAIEmbeddingResponse>();
        return result?.Data?.FirstOrDefault()?.Embedding ?? Array.Empty<float>();
    }

    public override async Task<bool> IsAvailableAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync(_baseUrl + "/v1/models");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public override async Task<ProviderStatus> GetStatusAsync()
    {
        return new ProviderStatus
        {
            ProviderId = ProviderId,
            Model = _model,
            IsAvailable = await IsAvailableAsync(),
            CheckedAt = DateTime.UtcNow
        };
    }
}

// ==================== OpenAI Provider ====================

/// <summary>
/// OpenAI provider - for OpenAI API and any OpenAI-compatible endpoint.
/// </summary>
public class OpenAILLMProvider : BaseLLMProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _model;
    private readonly string _apiKey;

    public OpenAILLMProvider(
        ILogger<OpenAILLMProvider> logger,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory)
        : base(logger, configuration)
    {
        _baseUrl = configuration["OpenAI:BaseUrl"] ?? "https://api.openai.com/v1";
        _model = configuration["OpenAI:Model"] ?? "gpt-4o-mini";
        _apiKey = configuration["OpenAI:ApiKey"] ?? throw new InvalidOperationException("OpenAI ApiKey is required");
        _httpClient = httpClientFactory.CreateClient();
        _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);
        _httpClient.DefaultRequestHeaders.Add("OpenAI-Organization", configuration["OpenAI:Organization"] ?? "");
    }

    public override string ProviderId => "openai";

    public override string EndpointUrl => _baseUrl;

    public override (int promptTokens, int completionTokens, int totalTokens)? GetTokenUsage(string responseText)
    {
        try
        {
            var usage = System.Text.Json.JsonSerializer.Deserialize<OpenAIUsage>(responseText);
            if (usage != null && usage.TotalTokens > 0)
            {
                return (usage.PromptTokens, usage.CompletionTokens, usage.TotalTokens);
            }
        }
        catch
        {
            // Response is not the usage object, ignore
        }
        return null;
    }

    public override async Task<string> CompleteAsync(string systemPrompt, string userPrompt, LLMOptions? options = null)
    {
        string modelName = options?.Model ?? _model;
        float temperature = options?.Temperature ?? 0.7f;
        int maxTokens = options?.MaxTokens ?? 2048;
        float topP = options?.TopP ?? 0.9f;

        var payload = new
        {
            model = modelName,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            stream = false,
            temperature,
            max_tokens = maxTokens,
            top_p = topP,
            frequency_penalty = options?.FrequencyPenalty,
            presence_penalty = options?.PresencePenalty
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                _baseUrl + "/chat/completions", payload);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("OpenAI API error: {Error}", error);
                return "Error: " + response.StatusCode;
            }

            var responseResult = await response.Content.ReadFromJsonAsync<OpenAIChatResponse>();
            return responseResult?.Choices?.FirstOrDefault()?.Message?.Content ?? "No response";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenAI request failed");
            return "Error: " + ex.Message;
        }
    }

    /// <summary>
    /// OpenAI-native tool calling implementation.
    /// Sends tools to the API and parses tool_call responses.
    /// </summary>
    public override async Task<LLMCompletionResult> CompleteWithToolsAsync(
        string systemPrompt, string userPrompt, IEnumerable<GMToolDefinition> tools, LLMOptions? options = null)
    {
        string modelName = options?.Model ?? _model;
        float temperature = options?.Temperature > 0 ? options.Temperature : 0.7f;
        int maxTokens = options?.MaxTokens > 0 ? options.MaxTokens : 2048;
        float topP = options?.TopP > 0 ? options.TopP : 0.9f;

        var toolDefs = tools.ToList();
        var toolsPayload = toolDefs.Select(t => t.ToOpenAISchema()).ToList();

        var payload = new
        {
            model = modelName,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            stream = false,
            temperature,
            max_tokens = maxTokens,
            top_p = topP,
            frequency_penalty = options?.FrequencyPenalty,
            presence_penalty = options?.PresencePenalty,
            tools = toolsPayload.Any() ? toolsPayload : null,
            tool_choice = toolsPayload.Any() ? new { type = "auto" } : null
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                _baseUrl + "/chat/completions", payload);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("OpenAI API error: {Error}", error);
                return new LLMCompletionResult { Content = "Error: " + response.StatusCode };
            }

            var responseText = await response.Content.ReadAsStringAsync();
            var result = await response.Content.ReadFromJsonAsync<OpenAIChatResponse>();

            var content = result?.Choices?.FirstOrDefault()?.Message?.Content ?? "No response";
            var toolCalls = result?.Choices?.FirstOrDefault()?.Message?.ToolCalls
                ?.Select(tc => new ToolCall
                {
                    Id = tc.Id ?? $"call_{Guid.NewGuid():N[..8]}",
                    Name = tc.Function?.Name ?? "unknown",
                    Arguments = tc.Function?.Arguments ?? "{}"
                }).ToList() ?? new List<ToolCall>();

            return new LLMCompletionResult
            {
                Content = content,
                ToolCalls = toolCalls,
                TokenUsage = GetTokenUsage(responseText)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenAI tool call request failed");
            return new LLMCompletionResult { Content = "Error: " + ex.Message };
        }
    }

    public override async Task<float[]> GetEmbeddingAsync(string text)
    {
        var payload = new { model = "text-embedding-3-small", input = text };
        var response = await _httpClient.PostAsJsonAsync(
            _baseUrl + "/embeddings", payload);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException("OpenAI embedding failed: " + response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<OpenAIEmbeddingResponse>();
        return result?.Data?.FirstOrDefault()?.Embedding ?? Array.Empty<float>();
    }

    public override async Task<bool> IsAvailableAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync(_baseUrl + "/models");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public override async Task<ProviderStatus> GetStatusAsync()
    {
        return new ProviderStatus
        {
            ProviderId = ProviderId,
            Model = _model,
            IsAvailable = await IsAvailableAsync(),
            CheckedAt = DateTime.UtcNow
        };
    }
}

// ==================== Google AI Studio Provider ====================

/// <summary>
/// Google AI Studio provider - for Google's AI models via their API.
/// </summary>
public class GoogleAIStudioLLMProvider : BaseLLMProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;

    public GoogleAIStudioLLMProvider(
        ILogger<GoogleAIStudioLLMProvider> logger,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory)
        : base(logger, configuration)
    {
        _apiKey = configuration["Google:ApiKey"] ?? throw new InvalidOperationException("Google AI Studio ApiKey is required");
        _model = configuration["Google:Model"] ?? "gemini-pro";
        _httpClient = httpClientFactory.CreateClient();
    }

    public override string ProviderId => "google";

    public override string EndpointUrl => $"https://generativelanguage.googleapis.com/v1beta/models/{_model}";

    public override (int promptTokens, int completionTokens, int totalTokens)? GetTokenUsage(string responseText)
    {
        try
        {
            // Google AI response structure: { "usageMetadata": { "promptTokenCount": 100, "candidatesTokenCount": 200, "totalTokenCount": 300 } }
            var doc = System.Text.Json.JsonDocument.Parse(responseText);
            var metadata = doc.RootElement.GetProperty("usageMetadata");
            var promptTokens = metadata.TryGetProperty("promptTokenCount", out var p) ? p.GetInt32() : 0;
            var completionTokens = metadata.TryGetProperty("candidatesTokenCount", out var c) ? c.GetInt32() : 0;
            var totalTokens = metadata.TryGetProperty("totalTokenCount", out var t) ? t.GetInt32() : 0;
            if (totalTokens > 0)
            {
                return (promptTokens, completionTokens, totalTokens);
            }
        }
        catch
        {
            // Response is not the usage object, ignore
        }
        return null;
    }

    public override async Task<string> CompleteAsync(string systemPrompt, string userPrompt, LLMOptions? options = null)
    {
        string modelName = options?.Model ?? _model;
        float temperature = options?.Temperature ?? 0.7f;
        int maxTokens = options?.MaxTokens ?? 2048;

        var payload = new
        {
            contents = new[]
            {
                new { role = "user", parts = new[] { new { text = userPrompt } } }
            },
            system_instruction = new { parts = new[] { new { text = systemPrompt } } },
            generationConfig = new
            {
                temperature,
                maxOutputTokens = maxTokens,
                topP = options?.TopP ?? 0.9f
            }
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent?key={_apiKey}", payload);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Google AI Studio API error: {Error}", error);
                return "Error: " + response.StatusCode;
            }

            var result = await response.Content.ReadFromJsonAsync<GoogleAIResponse>();
            return result?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text ?? "No response";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Google AI Studio request failed");
            return "Error: " + ex.Message;
        }
    }

    /// <summary>
    /// Google AI Studio tool calling implementation.
    /// Uses the Generative AI API's function calling format.
    /// </summary>
    public override async Task<LLMCompletionResult> CompleteWithToolsAsync(
        string systemPrompt, string userPrompt, IEnumerable<GMToolDefinition> tools, LLMOptions? options = null)
    {
        string modelName = options?.Model ?? _model;
        float temperature = options?.Temperature > 0 ? options.Temperature : 0.7f;
        int maxTokens = options?.MaxTokens > 0 ? options.MaxTokens : 2048;

        var toolDefs = tools.ToList();
        var googleTools = toolDefs.Any() ? new[]
        {
            new { functionDeclarations = toolDefs.Select(t => new
            {
                name = t.Name,
                description = t.Description,
                parameters = t.Parameters
            })
        } }
        : null;

        var payload = new
        {
            contents = new[]
            {
                new { role = "user", parts = new[] { new { text = userPrompt } } }
            },
            system_instruction = new { parts = new[] { new { text = systemPrompt } } },
            generationConfig = new
            {
                temperature,
                maxOutputTokens = maxTokens,
                topP = options?.TopP > 0 ? options.TopP : 0.9f
            },
            tools = googleTools,
            tool_config = googleTools != null ? new { function_config = new { call = new { function_names = toolDefs.Select(t => t.Name).ToArray() } } } : null
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent?key={_apiKey}", payload);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Google AI Studio tool call API error: {Error}", error);
                return new LLMCompletionResult { Content = "Error: " + response.StatusCode };
            }

            var responseText = await response.Content.ReadAsStringAsync();
            var result = await response.Content.ReadFromJsonAsync<GoogleAIResponse>();

            var content = result?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text ?? "No response";

            // Check for function calls
            var toolCalls = new List<ToolCall>();
            var candidate = result?.Candidates?.FirstOrDefault();
            if (candidate?.Content?.Parts != null)
            {
                foreach (var part in candidate.Content.Parts)
                {
                    if (part.FunctionCall != null)
                    {
                        toolCalls.Add(new ToolCall
                        {
                            Id = $"call_{Guid.NewGuid():N[..8]}",
                            Name = part.FunctionCall.Name ?? "unknown",
                            Arguments = JsonSerializer.Serialize(part.FunctionCall.Args ?? new())
                        });
                    }
                }
            }

            return new LLMCompletionResult
            {
                Content = content,
                ToolCalls = toolCalls,
                TokenUsage = GetTokenUsage(responseText)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Google AI Studio tool call request failed");
            return new LLMCompletionResult { Content = "Error: " + ex.Message };
        }
    }

    public override async Task<float[]> GetEmbeddingAsync(string text)
    {
        var payload = new { content = new { parts = new[] { new { text } } } };
        var response = await _httpClient.PostAsJsonAsync(
            $"https://generativelanguage.googleapis.com/v1beta/models/text-embedding-004:embedContent?key={_apiKey}", payload);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException("Google AI Studio embedding failed: " + response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<GoogleAIEmbeddingResponse>();
        return result?.Embedding?.Values ?? Array.Empty<float>();
    }

    public override async Task<bool> IsAvailableAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"https://generativelanguage.googleapis.com/v1beta/models?key={_apiKey}");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public override async Task<ProviderStatus> GetStatusAsync()
    {
        return new ProviderStatus
        {
            ProviderId = ProviderId,
            Model = _model,
            IsAvailable = await IsAvailableAsync(),
            CheckedAt = DateTime.UtcNow
        };
    }
}

// ==================== Preset-based Provider Wrappers ====================

/// <summary>
/// Wrapper that creates an Ollama provider from a user preset.
/// </summary>
public class OllamaLLMProviderFromPreset : BaseLLMProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _model;

    public OllamaLLMProviderFromPreset(LLMPreset preset)
        : base(new NullLogger<OllamaLLMProviderFromPreset>(), new NullConfiguration())
    {
        _baseUrl = preset.EndpointUrl ?? "http://localhost:11434";
        _model = preset.BaseModel;
        _httpClient = new HttpClient();
    }

    public override string ProviderId => "ollama";

    public override string EndpointUrl => _baseUrl;

    public override (int promptTokens, int completionTokens, int totalTokens)? GetTokenUsage(string responseText)
    {
        try
        {
            // Ollama doesn't return token counts in chat responses
            return null;
        }
        catch
        {
            return null;
        }
    }

    public override async Task<string> CompleteAsync(string systemPrompt, string userPrompt, LLMOptions? options = null)
    {
        string modelName = options?.Model ?? _model;
        float temperature = options?.Temperature > 0 ? options.Temperature : 0.7f;
        int maxTokens = options?.MaxTokens > 0 ? options.MaxTokens : 2048;

        var payload = new
        {
            model = modelName,
            system = systemPrompt,
            prompt = userPrompt,
            stream = false,
            opts = new
            {
                temperature,
                num_predict = maxTokens
            }
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                _baseUrl + "/api/chat", payload,
                new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                return "Error: " + response.StatusCode;
            }

            var result = await response.Content.ReadFromJsonAsync<OllamaResponse>();
            return result?.Message?.Content ?? "No response";
        }
        catch (Exception ex)
        {
            return "Error: " + ex.Message;
        }
    }

    public override Task<float[]> GetEmbeddingAsync(string text) =>
        Task.FromResult(Array.Empty<float>());
    public override Task<bool> IsAvailableAsync() => Task.FromResult(true);
    public override Task<ProviderStatus> GetStatusAsync() =>
        Task.FromResult(new ProviderStatus { ProviderId = ProviderId, Model = _model, IsAvailable = true, CheckedAt = DateTime.UtcNow });
}

/// <summary>
/// Wrapper that creates an LM Studio provider from a user preset.
/// </summary>
public class LmStudioLLMProviderFromPreset : BaseLLMProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _model;

    public LmStudioLLMProviderFromPreset(LLMPreset preset)
        : base(new NullLogger<LmStudioLLMProviderFromPreset>(), new NullConfiguration())
    {
        _baseUrl = preset.EndpointUrl ?? "http://localhost:1234";
        _model = preset.BaseModel;
        _httpClient = new HttpClient();
        if (!string.IsNullOrEmpty(preset.ApiKey))
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", preset.ApiKey);
    }

    public override string ProviderId => "lmstudio";

    public override string EndpointUrl => _baseUrl;

    public override (int promptTokens, int completionTokens, int totalTokens)? GetTokenUsage(string responseText)
    {
        try
        {
            var usage = System.Text.Json.JsonSerializer.Deserialize<OpenAIUsage>(responseText);
            if (usage != null && usage.TotalTokens > 0)
            {
                return (usage.PromptTokens, usage.CompletionTokens, usage.TotalTokens);
            }
        }
        catch
        {
            // Response is not the usage object, ignore
        }
        return null;
    }

    public override async Task<string> CompleteAsync(string systemPrompt, string userPrompt, LLMOptions? options = null)
    {
        string modelName = options?.Model ?? _model;
        float temperature = options?.Temperature > 0 ? options.Temperature : 0.7f;
        int maxTokens = options?.MaxTokens > 0 ? options.MaxTokens : 2048;
        float topP = options?.TopP > 0 ? options.TopP : 0.9f;

        var payload = new
        {
            model = modelName,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            stream = false,
            temperature,
            max_tokens = maxTokens,
            top_p = topP,
            frequency_penalty = options?.FrequencyPenalty,
            presence_penalty = options?.PresencePenalty
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                _baseUrl + "/v1/chat/completions", payload);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                return "Error: " + response.StatusCode;
            }

            var result = await response.Content.ReadFromJsonAsync<OpenAIChatResponse>();
            return result?.Choices?.FirstOrDefault()?.Message?.Content ?? "No response";
        }
        catch (Exception ex)
        {
            return "Error: " + ex.Message;
        }
    }

    public override Task<float[]> GetEmbeddingAsync(string text) =>
        Task.FromResult(Array.Empty<float>());
    public override Task<bool> IsAvailableAsync() => Task.FromResult(true);
    public override Task<ProviderStatus> GetStatusAsync() =>
        Task.FromResult(new ProviderStatus { ProviderId = ProviderId, Model = _model, IsAvailable = true, CheckedAt = DateTime.UtcNow });
}

/// <summary>
/// Wrapper that creates an OpenAI provider from a user preset.
/// </summary>
public class OpenAILLMProviderFromPreset : BaseLLMProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _model;
    private readonly string _apiKey;

    public OpenAILLMProviderFromPreset(LLMPreset preset)
        : base(new NullLogger<OpenAILLMProviderFromPreset>(), new NullConfiguration())
    {
        _baseUrl = preset.EndpointUrl ?? "https://api.openai.com/v1";
        _model = preset.BaseModel;
        _apiKey = preset.ApiKey ?? throw new InvalidOperationException("API key is required");
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);
    }

    public override string ProviderId => "openai";

    public override string EndpointUrl => _baseUrl;

    public override (int promptTokens, int completionTokens, int totalTokens)? GetTokenUsage(string responseText)
    {
        try
        {
            var usage = System.Text.Json.JsonSerializer.Deserialize<OpenAIUsage>(responseText);
            if (usage != null && usage.TotalTokens > 0)
            {
                return (usage.PromptTokens, usage.CompletionTokens, usage.TotalTokens);
            }
        }
        catch
        {
            // Response is not the usage object, ignore
        }
        return null;
    }

    public override async Task<string> CompleteAsync(string systemPrompt, string userPrompt, LLMOptions? options = null)
    {
        string modelName = options?.Model ?? _model;
        float temperature = options?.Temperature > 0 ? options.Temperature : 0.7f;
        int maxTokens = options?.MaxTokens > 0 ? options.MaxTokens : 2048;
        float topP = options?.TopP > 0 ? options.TopP : 0.9f;

        var payload = new
        {
            model = modelName,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            stream = false,
            temperature,
            max_tokens = maxTokens,
            top_p = topP,
            frequency_penalty = options?.FrequencyPenalty,
            presence_penalty = options?.PresencePenalty
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                _baseUrl + "/chat/completions", payload);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                return "Error: " + response.StatusCode;
            }

            var result = await response.Content.ReadFromJsonAsync<OpenAIChatResponse>();
            return result?.Choices?.FirstOrDefault()?.Message?.Content ?? "No response";
        }
        catch (Exception ex)
        {
            return "Error: " + ex.Message;
        }
    }

    public override Task<float[]> GetEmbeddingAsync(string text) =>
        Task.FromResult(Array.Empty<float>());
    public override Task<bool> IsAvailableAsync() => Task.FromResult(true);
    public override Task<ProviderStatus> GetStatusAsync() =>
        Task.FromResult(new ProviderStatus { ProviderId = ProviderId, Model = _model, IsAvailable = true, CheckedAt = DateTime.UtcNow });
}

/// <summary>
/// Wrapper that creates a Google AI Studio provider from a user preset.
/// </summary>
public class GoogleAIStudioLLMProviderFromPreset : BaseLLMProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;

    public GoogleAIStudioLLMProviderFromPreset(LLMPreset preset)
        : base(new NullLogger<GoogleAIStudioLLMProviderFromPreset>(), new NullConfiguration())
    {
        _apiKey = preset.ApiKey ?? throw new InvalidOperationException("API key is required");
        _model = preset.BaseModel;
        _httpClient = new HttpClient();
    }

    public override string ProviderId => "google";

    public override string EndpointUrl => $"https://generativelanguage.googleapis.com/v1beta/models/{_model}";

    public override (int promptTokens, int completionTokens, int totalTokens)? GetTokenUsage(string responseText)
    {
        try
        {
            // Google AI response structure: { "usageMetadata": { "promptTokenCount": 100, "candidatesTokenCount": 200, "totalTokenCount": 300 } }
            var doc = System.Text.Json.JsonDocument.Parse(responseText);
            var metadata = doc.RootElement.GetProperty("usageMetadata");
            var promptTokens = metadata.TryGetProperty("promptTokenCount", out var p) ? p.GetInt32() : 0;
            var completionTokens = metadata.TryGetProperty("candidatesTokenCount", out var c) ? c.GetInt32() : 0;
            var totalTokens = metadata.TryGetProperty("totalTokenCount", out var t) ? t.GetInt32() : 0;
            if (totalTokens > 0)
            {
                return (promptTokens, completionTokens, totalTokens);
            }
        }
        catch
        {
            // Response is not the usage object, ignore
        }
        return null;
    }

    public override async Task<string> CompleteAsync(string systemPrompt, string userPrompt, LLMOptions? options = null)
    {
        string modelName = options?.Model ?? _model;
        float temperature = options?.Temperature > 0 ? options.Temperature : 0.7f;
        int maxTokens = options?.MaxTokens > 0 ? options.MaxTokens : 2048;

        var payload = new
        {
            contents = new[]
            {
                new { role = "user", parts = new[] { new { text = userPrompt } } }
            },
            system_instruction = new { parts = new[] { new { text = systemPrompt } } },
            generationConfig = new
            {
                temperature,
                maxOutputTokens = maxTokens,
                topP = options?.TopP > 0 ? options.TopP : 0.9f
            }
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent?key={_apiKey}", payload);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                return "Error: " + response.StatusCode;
            }

            var result = await response.Content.ReadFromJsonAsync<GoogleAIResponse>();
            return result?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text ?? "No response";
        }
        catch (Exception ex)
        {
            return "Error: " + ex.Message;
        }
    }

    public override Task<float[]> GetEmbeddingAsync(string text) =>
        Task.FromResult(Array.Empty<float>());
    public override Task<bool> IsAvailableAsync() => Task.FromResult(true);
    public override Task<ProviderStatus> GetStatusAsync() =>
        Task.FromResult(new ProviderStatus { ProviderId = ProviderId, Model = _model, IsAvailable = true, CheckedAt = DateTime.UtcNow });
}

// ==================== API Response Types ====================

public class OpenAIChatResponse
{
    public List<OpenAIChatChoice>? Choices { get; set; }
    public OpenAIUsage? Usage { get; set; }
}

public class OpenAIChatChoice
{
    public OpenAIChatMessage? Message { get; set; }
}

public class OpenAIChatMessage
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public List<OpenAIToolCall>? ToolCalls { get; set; }
}

public class OpenAIToolCall
{
    public string? Id { get; set; }
    public string? Type { get; set; }
    public OpenAIFunctionCall? Function { get; set; }
}

public class OpenAIFunctionCall
{
    public string? Name { get; set; }
    public string? Arguments { get; set; }
}

public class OpenAIUsage
{
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
}

public class OpenAIEmbeddingResponse
{
    public List<OpenAIEmbeddingData>? Data { get; set; }
}

public class OpenAIEmbeddingData
{
    public float[]? Embedding { get; set; }
}

public class GoogleAIResponse
{
    public List<GoogleAICandidate>? Candidates { get; set; }
}

public class GoogleAICandidate
{
    public GoogleAIContent? Content { get; set; }
}

public class GoogleAIContent
{
    public List<GoogleAIPart>? Parts { get; set; }
}

public class GoogleAIPart
{
    public string? Text { get; set; }
    public GoogleAIFunctionCall? FunctionCall { get; set; }
}

public class GoogleAIFunctionCall
{
    public string? Name { get; set; }
    public Dictionary<string, object>? Args { get; set; }
}

public class GoogleAIEmbeddingResponse
{
    public GoogleAIEmbedding? Embedding { get; set; }
}

public class GoogleAIEmbedding
{
    public float[]? Values { get; set; }
}

// ==================== Null helpers ====================

internal class NullLogger<T> : ILogger<T>
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => false;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
}

#pragma warning disable CS8767 // Nullability of reference types in type of indexer/Value setter doesn't match implicitly implemented member (null impl)
internal class NullConfiguration : IConfiguration
{
    public string this[string key] { get => string.Empty; set { } }
    public IEnumerable<IConfigurationSection> GetChildren() => Enumerable.Empty<IConfigurationSection>();
    public IConfigurationSection GetSection(string key) => new NullConfigurationSection();
    public IChangeToken GetReloadToken() => new NullChangeToken();
    public void Bind(object obj) { }
}

internal class NullConfigurationSection : IConfigurationSection
{
    public string Key => "";
    public string Path => "";
    public string Value { get => string.Empty; set { } }
    public string this[string key] { get => string.Empty; set { } }
    public IEnumerable<IConfigurationSection> GetChildren() => Enumerable.Empty<IConfigurationSection>();
    public IConfigurationSection GetSection(string key) => this;
    public IChangeToken GetReloadToken() => new NullChangeToken();
}
#pragma warning restore CS8767

internal class NullChangeToken : IChangeToken
{
    public bool HasChanged => false;
    public bool ActiveChangeCallbacks => false;
    public IDisposable RegisterChangeCallback(Action<object> callback, object? state) => null!;
    public void GetChangeToken() => throw new NotImplementedException();
}

// Ollama API response types
public class OllamaResponse
{
    public OllamaMessage? Message { get; set; }
    public bool Done { get; set; }
}

public class OllamaMessage
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public List<OllamaToolCall>? ToolCalls { get; set; }
}

public class OllamaToolCall
{
    public string? Id { get; set; }
    public OllamaFunctionCall? Function { get; set; }
}

public class OllamaFunctionCall
{
    public string? Name { get; set; }
    public string? Arguments { get; set; }
}

public class OllamaEmbeddingResponse
{
    public float[]? Embedding { get; set; }
}
