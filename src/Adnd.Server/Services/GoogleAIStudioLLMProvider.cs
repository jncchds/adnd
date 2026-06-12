using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Http;
using Adnd.Server.Models;

namespace Adnd.Server.Services;

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

    /// <summary>
    /// Google AI Studio tool calling implementation.
    /// Uses the Generative AI API's function calling format.
    /// </summary>
    protected override async Task<LLMCompletionResult> CompleteWithToolsAsyncCore(
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
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var response = await _httpClient.PostAsJsonAsync(
                $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent?key={_apiKey}", payload);
            sw.Stop();

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("[PROVIDER] CompleteWithToolsAsync | Provider=google | Model={Model} | HTTP={StatusCode} | Duration={Duration}ms | Error={Error}",
                    modelName, (int)response.StatusCode, sw.ElapsedMilliseconds, error);
                return new LLMCompletionResult { Content = $"Error: {response.StatusCode}" };
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

            _logger.LogInformation("[PROVIDER] CompleteWithToolsAsync | Provider=google | Model={Model} | Duration={Duration}ms | ResponseLen={ResponseLen} | ToolCalls={ToolCalls}",
                modelName, sw.ElapsedMilliseconds, content.Length, toolCalls.Count);

            return new LLMCompletionResult
            {
                Content = content,
                ToolCalls = toolCalls,
                TokenUsage = GetTokenUsage(responseText)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PROVIDER] CompleteWithToolsAsync | Provider=google | Model={Model} | Error={Error}",
                modelName, ex.Message);
            return new LLMCompletionResult { Content = $"Error: {ex.Message}" };
        }
    }

    protected override async Task<float[]> GetEmbeddingAsyncCore(string text)
    {
        var payload = new { content = new { parts = new[] { new { text } } } };
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var response = await _httpClient.PostAsJsonAsync(
            $"https://generativelanguage.googleapis.com/v1beta/models/text-embedding-004:embedContent?key={_apiKey}", payload);
        sw.Stop();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("[PROVIDER] GetEmbeddingAsync | Provider=google | Model={Model} | HTTP={StatusCode} | Duration={Duration}ms", "text-embedding-004", (int)response.StatusCode, sw.ElapsedMilliseconds);
            throw new InvalidOperationException("Google AI Studio embedding failed: " + response.StatusCode);
        }

        var result = await response.Content.ReadFromJsonAsync<GoogleAIEmbeddingResponse>();
        var embedding = result?.Embedding?.Values ?? Array.Empty<float>();

        _logger.LogInformation("[PROVIDER] GetEmbeddingAsync | Provider=google | Model={Model} | TextLen={TextLen} | Dim={Dim} | Duration={Duration}ms",
            "text-embedding-004", text.Length, embedding.Length, sw.ElapsedMilliseconds);

        return embedding;
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
