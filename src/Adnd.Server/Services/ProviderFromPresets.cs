namespace Adnd.Server.Services;

using Adnd.Server.Models;

public class OllamaLLMProviderFromPreset : BaseLLMProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _model;
    private readonly string _embeddingUrl;
    private readonly string _embeddingModel;

    public OllamaLLMProviderFromPreset(LLMPreset preset, ILogger<BaseLLMProvider>? logger = null, IConfiguration? configuration = null, IHttpClientFactory? httpClientFactory = null)
        : base(logger ?? new NullLogger<OllamaLLMProviderFromPreset>(), configuration ?? new NullConfiguration())
    {
        _baseUrl = preset.EndpointUrl ?? "http://localhost:11434";
        _model = preset.BaseModel;
        _embeddingUrl = preset.EmbeddingEndpointUrl ?? _baseUrl;
        _embeddingModel = preset.EmbeddingModel ?? "nomic-embed-text";
        // Use IHttpClientFactory for connection pooling — falls back to new HttpClient() if not provided
        _httpClient = httpClientFactory?.CreateClient("LLMProvider") ?? new HttpClient();
    }

    public override string ProviderId => "ollama";

    public override string EndpointUrl => _baseUrl;

    public override string ModelName => _model;

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

    protected override async Task<string> CompleteAsyncCore(string systemPrompt, string userPrompt, LLMOptions? options = null)
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
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var response = await _httpClient.PostAsJsonAsync(
                _baseUrl + "/api/chat", payload,
                new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
            sw.Stop();

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("[PROVIDER] CompleteAsync | Provider=ollama | Model={Model} | HTTP={StatusCode} | Duration={Duration}ms | Error={Error}",
                    modelName, (int)response.StatusCode, sw.ElapsedMilliseconds, error);
                return $"Error: {response.StatusCode}";
            }

            var result = await response.Content.ReadFromJsonAsync<OllamaResponse>();
            var content = result?.Message?.Content ?? "No response";

            _logger.LogInformation("[PROVIDER] CompleteAsync | Provider=ollama | Model={Model} | Duration={Duration}ms | ResponseLen={ResponseLen}",
                modelName, sw.ElapsedMilliseconds, content.Length);

            return content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PROVIDER] CompleteAsync | Provider=ollama | Model={Model} | Error={Error}",
                modelName, ex.Message);
            return $"Error: {ex.Message}";
        }
    }

    protected override async Task<float[]> GetEmbeddingAsyncCore(string text)
    {
        var payload = new { model = _embeddingModel, input = text };
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var response = await _httpClient.PostAsJsonAsync(
            _embeddingUrl + "/api/embed", payload);
        sw.Stop();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("[PROVIDER] GetEmbeddingAsync | Provider=ollama | Model={Model} | HTTP={StatusCode} | Duration={Duration}ms", _embeddingModel, (int)response.StatusCode, sw.ElapsedMilliseconds);
            return Array.Empty<float>();
        }

        var result = await response.Content.ReadFromJsonAsync<OllamaEmbeddingResponse>();
        var embedding = result?.Embedding ?? Array.Empty<float>();

        _logger.LogInformation("[PROVIDER] GetEmbeddingAsync | Provider=ollama | Model={Model} | TextLen={TextLen} | Dim={Dim} | Duration={Duration}ms",
            _embeddingModel, text.Length, embedding.Length, sw.ElapsedMilliseconds);

        return embedding;
    }
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
    private readonly string _embeddingUrl;
    private readonly string _embeddingModel;

    public LmStudioLLMProviderFromPreset(LLMPreset preset, ILogger<BaseLLMProvider>? logger = null, IConfiguration? configuration = null, IHttpClientFactory? httpClientFactory = null)
        : base(logger ?? new NullLogger<LmStudioLLMProviderFromPreset>(), configuration ?? new NullConfiguration())
    {
        _baseUrl = preset.EndpointUrl ?? "http://localhost:1234";
        _model = preset.BaseModel;
        _embeddingUrl = preset.EmbeddingEndpointUrl ?? _baseUrl;
        _embeddingModel = preset.EmbeddingModel ?? "nomic-embed-text";
        // Use IHttpClientFactory for connection pooling — falls back to new HttpClient() if not provided
        _httpClient = httpClientFactory?.CreateClient("LLMProvider") ?? new HttpClient();
        var apiKey = preset.DecryptedApiKey ?? preset.ApiKey;
        if (!string.IsNullOrEmpty(apiKey))
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
    }

    public override string ProviderId => "lmstudio";

    public override string EndpointUrl => _baseUrl;

    public override string ModelName => _model;

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

    protected override async Task<string> CompleteAsyncCore(string systemPrompt, string userPrompt, LLMOptions? options = null)
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
            presence_penalty = options?.PresencePenalty,
            tool_choice = "none"  // Disable tool-calling for plain text responses
        };

        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var response = await _httpClient.PostAsJsonAsync(
                _baseUrl + "/v1/chat/completions", payload);
            sw.Stop();

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("[PROVIDER] CompleteAsync | Provider=lmstudio | Model={Model} | HTTP={StatusCode} | Duration={Duration}ms | Error={Error}",
                    modelName, (int)response.StatusCode, sw.ElapsedMilliseconds, error);
                return $"Error: {response.StatusCode}";
            }

            var result = await response.Content.ReadFromJsonAsync<OpenAIChatResponse>();
            var message = result?.Choices?.FirstOrDefault()?.Message;
            if (message == null)
            {
                _logger.LogWarning("[PROVIDER] CompleteAsync | Provider=lmstudio | Model={Model} | Duration={Duration}ms | NoChoicesInResponse", modelName, sw.ElapsedMilliseconds);
                return "No response";
            }
            // Some models (reasoning models) output reasoning_content instead of content
            var content = message.Content;
            if (string.IsNullOrEmpty(content) && message is { ReasoningContent: not null })
            {
                content = message.ReasoningContent;
            }

            _logger.LogInformation("[PROVIDER] CompleteAsync | Provider=lmstudio | Model={Model} | Duration={Duration}ms | ResponseLen={ResponseLen}",
                modelName, sw.ElapsedMilliseconds, content?.Length ?? 0);

            return content ?? "No response";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PROVIDER] CompleteAsync | Provider=lmstudio | Model={Model} | Error={Error}",
                modelName, ex.Message);
            return $"Error: {ex.Message}";
        }
    }

    protected override async Task<float[]> GetEmbeddingAsyncCore(string text)
    {
        var embeddingModel = _embeddingModel ?? "nomic-embed-text";
        var payload = new { model = embeddingModel, input = text };
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var response = await _httpClient.PostAsJsonAsync(
            _embeddingUrl + "/v1/embeddings", payload);
        sw.Stop();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("[PROVIDER] GetEmbeddingAsync | Provider=lmstudio | Model={Model} | HTTP={StatusCode} | Duration={Duration}ms", embeddingModel, (int)response.StatusCode, sw.ElapsedMilliseconds);
            return Array.Empty<float>();
        }

        var result = await response.Content.ReadFromJsonAsync<OpenAIEmbeddingResponse>();
        var embedding = result?.Data?.FirstOrDefault()?.Embedding ?? Array.Empty<float>();

        _logger.LogInformation("[PROVIDER] GetEmbeddingAsync | Provider=lmstudio | Model={Model} | TextLen={TextLen} | Dim={Dim} | Duration={Duration}ms",
            embeddingModel, text.Length, embedding.Length, sw.ElapsedMilliseconds);

        return embedding;
    }
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
    private readonly string _embeddingUrl;
    private readonly string _embeddingModel;

    public OpenAILLMProviderFromPreset(LLMPreset preset, ILogger<BaseLLMProvider>? logger = null, IConfiguration? configuration = null, IHttpClientFactory? httpClientFactory = null)
        : base(logger ?? new NullLogger<OpenAILLMProviderFromPreset>(), configuration ?? new NullConfiguration())
    {
        _baseUrl = preset.EndpointUrl ?? "https://api.openai.com/v1";
        _model = preset.BaseModel;
        _apiKey = preset.DecryptedApiKey ?? preset.ApiKey ?? throw new InvalidOperationException("API key is required");
        _embeddingUrl = preset.EmbeddingEndpointUrl ?? _baseUrl;
        _embeddingModel = preset.EmbeddingModel ?? "text-embedding-3-small";
        // Use IHttpClientFactory for connection pooling — falls back to new HttpClient() if not provided
        _httpClient = httpClientFactory?.CreateClient("LLMProvider") ?? new HttpClient();
        _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);
    }

    public override string ProviderId => "openai";

    public override string EndpointUrl => _baseUrl;

    public override string ModelName => _model;

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

    protected override async Task<string> CompleteAsyncCore(string systemPrompt, string userPrompt, LLMOptions? options = null)
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
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var response = await _httpClient.PostAsJsonAsync(
                _baseUrl + "/chat/completions", payload);
            sw.Stop();

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("[PROVIDER] CompleteAsync | Provider=openai | Model={Model} | HTTP={StatusCode} | Duration={Duration}ms | Error={Error}",
                    modelName, (int)response.StatusCode, sw.ElapsedMilliseconds, error);
                return $"Error: {response.StatusCode}";
            }

            var result = await response.Content.ReadFromJsonAsync<OpenAIChatResponse>();
            var content = result?.Choices?.FirstOrDefault()?.Message?.Content ?? "No response";

            _logger.LogInformation("[PROVIDER] CompleteAsync | Provider=openai | Model={Model} | Duration={Duration}ms | ResponseLen={ResponseLen}",
                modelName, sw.ElapsedMilliseconds, content.Length);

            return content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PROVIDER] CompleteAsync | Provider=openai | Model={Model} | Error={Error}",
                modelName, ex.Message);
            return $"Error: {ex.Message}";
        }
    }

    protected override async Task<float[]> GetEmbeddingAsyncCore(string text)
    {
        var embeddingModel = _embeddingModel ?? "text-embedding-3-small";
        var payload = new { model = embeddingModel, input = new[] { text } };
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var response = await _httpClient.PostAsJsonAsync(
            _embeddingUrl + "/v1/embeddings", payload);
        sw.Stop();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("[PROVIDER] GetEmbeddingAsync | Provider=openai | Model={Model} | HTTP={StatusCode} | Duration={Duration}ms", embeddingModel, (int)response.StatusCode, sw.ElapsedMilliseconds);
            return Array.Empty<float>();
        }

        var result = await response.Content.ReadFromJsonAsync<OpenAIEmbeddingResponse>();
        var embedding = result?.Data?.FirstOrDefault()?.Embedding ?? Array.Empty<float>();

        _logger.LogInformation("[PROVIDER] GetEmbeddingAsync | Provider=openai | Model={Model} | TextLen={TextLen} | Dim={Dim} | Duration={Duration}ms",
            embeddingModel, text.Length, embedding.Length, sw.ElapsedMilliseconds);

        return embedding;
    }
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
    private readonly string _embeddingUrl;
    private readonly string _embeddingModel;

    public GoogleAIStudioLLMProviderFromPreset(LLMPreset preset, ILogger<BaseLLMProvider>? logger = null, IConfiguration? configuration = null, IHttpClientFactory? httpClientFactory = null)
        : base(logger ?? new NullLogger<GoogleAIStudioLLMProviderFromPreset>(), configuration ?? new NullConfiguration())
    {
        _apiKey = preset.DecryptedApiKey ?? preset.ApiKey ?? throw new InvalidOperationException("API key is required");
        _model = preset.BaseModel;
        _embeddingUrl = preset.EmbeddingEndpointUrl ?? "https://generativelanguage.googleapis.com";
        _embeddingModel = preset.EmbeddingModel ?? "text-embedding-004";
        // Use IHttpClientFactory for connection pooling — falls back to new HttpClient() if not provided
        _httpClient = httpClientFactory?.CreateClient("LLMProvider") ?? new HttpClient();
    }

    public override string ProviderId => "google";

    public override string EndpointUrl => $"https://generativelanguage.googleapis.com/v1beta/models/{_model}";

    public override string ModelName => _model;

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

    protected override async Task<string> CompleteAsyncCore(string systemPrompt, string userPrompt, LLMOptions? options = null)
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
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var response = await _httpClient.PostAsJsonAsync(
                $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent?key={_apiKey}", payload);
            sw.Stop();

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("[PROVIDER] CompleteAsync | Provider=google | Model={Model} | HTTP={StatusCode} | Duration={Duration}ms | Error={Error}",
                    modelName, (int)response.StatusCode, sw.ElapsedMilliseconds, error);
                return $"Error: {response.StatusCode}";
            }

            var result = await response.Content.ReadFromJsonAsync<GoogleAIResponse>();
            var content = result?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text ?? "No response";

            _logger.LogInformation("[PROVIDER] CompleteAsync | Provider=google | Model={Model} | Duration={Duration}ms | ResponseLen={ResponseLen}",
                modelName, sw.ElapsedMilliseconds, content.Length);

            return content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PROVIDER] CompleteAsync | Provider=google | Model={Model} | Error={Error}",
                modelName, ex.Message);
            return $"Error: {ex.Message}";
        }
    }

    protected override async Task<float[]> GetEmbeddingAsyncCore(string text)
    {
        var embeddingModel = _embeddingModel ?? "text-embedding-004";
        var payload = new { content = new { parts = new[] { new { text } } } };
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var response = await _httpClient.PostAsJsonAsync(
            $"{_embeddingUrl}/v1beta/models/{embeddingModel}:embedContent?key={_apiKey}", payload);
        sw.Stop();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("[PROVIDER] GetEmbeddingAsync | Provider=google | Model={Model} | HTTP={StatusCode} | Duration={Duration}ms", embeddingModel, (int)response.StatusCode, sw.ElapsedMilliseconds);
            return Array.Empty<float>();
        }

        var result = await response.Content.ReadFromJsonAsync<GoogleAIEmbeddingResponse>();
        var embedding = result?.Embedding?.Values ?? Array.Empty<float>();

        _logger.LogInformation("[PROVIDER] GetEmbeddingAsync | Provider=google | Model={Model} | TextLen={TextLen} | Dim={Dim} | Duration={Duration}ms",
            embeddingModel, text.Length, embedding.Length, sw.ElapsedMilliseconds);

        return embedding;
    }
    public override Task<bool> IsAvailableAsync() => Task.FromResult(true);
    public override Task<ProviderStatus> GetStatusAsync() =>
        Task.FromResult(new ProviderStatus { ProviderId = ProviderId, Model = _model, IsAvailable = true, CheckedAt = DateTime.UtcNow });
}

// ==================== API Response Types ====================
