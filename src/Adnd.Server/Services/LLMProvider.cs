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
    /// Generate text completion with optional system prompt.
    /// </summary>
    Task<string> CompleteAsync(string systemPrompt, string userPrompt, LLMOptions? options = null);

    /// <summary>
    /// Generate text in a structured format (JSON).
    /// </summary>
    Task<T> CompleteStructuredAsync<T>(string systemPrompt, string userPrompt, LLMOptions? options = null);

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

            var result = await response.Content.ReadFromJsonAsync<OpenAIChatResponse>();
            return result?.Choices?.FirstOrDefault()?.Message?.Content ?? "No response";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LM Studio request failed");
            return "Error: " + ex.Message;
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

            var result = await response.Content.ReadFromJsonAsync<OpenAIChatResponse>();
            return result?.Choices?.FirstOrDefault()?.Message?.Content ?? "No response";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenAI request failed");
            return "Error: " + ex.Message;
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
}

public class OllamaEmbeddingResponse
{
    public float[]? Embedding { get; set; }
}
