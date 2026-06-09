using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Http;
using Adnd.Server.Models;

namespace Adnd.Server.Services;

/// <summary>
/// Ollama provider for local/running LLM instances.
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
