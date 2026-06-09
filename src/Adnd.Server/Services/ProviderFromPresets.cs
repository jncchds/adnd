namespace Adnd.Server.Services;

using Adnd.Server.Models;

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
            var message = result?.Choices?.FirstOrDefault()?.Message;
            if (message == null) return "No response";
            // Some models (reasoning models) output reasoning_content instead of content
            var content = message.Content;
            if (string.IsNullOrEmpty(content) && message is { ReasoningContent: not null })
            {
                content = message.ReasoningContent;
            }
            return content ?? "No response";
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
