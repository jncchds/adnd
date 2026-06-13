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
        float temperature = options?.Temperature ?? 0.7f;
        int maxTokens = options?.MaxTokens ?? 2048;

        // Build payload for Ollama
        var opts = new Dictionary<string, object>
        {
            { "temperature", temperature },
            { "num_predict", maxTokens }
        };
        if (options?.JsonSchemaOutput != null)
        {
            opts["format"] = "json";
        }

        var payload = new
        {
            model = modelName,
            system = systemPrompt,
            prompt = userPrompt,
            stream = false,
            opts = opts
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

    /// <summary>
    /// Ollama-native tool calling implementation.
    /// Uses Ollama's /api/chat endpoint with the tools parameter.
    /// </summary>
    protected override async Task<LLMCompletionResult> CompleteWithToolsAsyncCore(
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

    protected override async Task<float[]> GetEmbeddingAsyncCore(string text)
    {
        string embeddingModel = "nomic-embed-text";
        var payload = new { model = embeddingModel, input = text };

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var response = await _httpClient.PostAsJsonAsync(
            _baseUrl + "/api/embed", payload);
        sw.Stop();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("[PROVIDER] GetEmbeddingAsync | Provider=ollama | Model={Model} | HTTP={StatusCode} | Duration={Duration}ms", embeddingModel, (int)response.StatusCode, sw.ElapsedMilliseconds);
            throw new InvalidOperationException("Ollama embedding failed: " + response.StatusCode);
        }

        var result = await response.Content.ReadFromJsonAsync<OllamaEmbeddingResponse>();
        var embedding = result?.Embedding ?? Array.Empty<float>();

        _logger.LogInformation("[PROVIDER] GetEmbeddingAsync | Provider=ollama | Model={Model} | TextLen={TextLen} | Dim={Dim} | Duration={Duration}ms",
            embeddingModel, text.Length, embedding.Length, sw.ElapsedMilliseconds);

        return embedding;
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
