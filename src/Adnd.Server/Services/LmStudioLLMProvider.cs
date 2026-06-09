using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Http;
using Adnd.Server.Models;

namespace Adnd.Server.Services;

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
