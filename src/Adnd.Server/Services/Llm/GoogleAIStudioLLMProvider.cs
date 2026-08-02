using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Adnd.Server.Services.Llm;

public class GoogleAIStudioLLMProvider(
    string apiKey,
    string model,
    string? embeddingModel,
    HttpClient httpClient,
    ILogger<GoogleAIStudioLLMProvider> logger) : BaseLLMProvider
{
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models";
    private const string DefaultEmbeddingModel = "text-embedding-004";

    // The preset's embedding model was ignored entirely and this was pinned.
    private readonly string _embeddingModel =
        string.IsNullOrWhiteSpace(embeddingModel) ? DefaultEmbeddingModel : embeddingModel;

    public override string ProviderId => "google";
    public override string EndpointUrl => BaseUrl;

    private int GetThinkingBudget(string reasoningEffort) =>
        reasoningEffort.ToLowerInvariant() switch
        {
            "low" => 1024,
            "medium" => 4096,
            "high" => 8192,
            _ => 0
        };

    protected override async Task<string> CompleteAsyncCore(string systemPrompt, string userPrompt, LLMOptions opts, CancellationToken ct)
    {
        var targetModel = string.IsNullOrEmpty(opts.Model) ? model : opts.Model;
        var result = await SendRequestAsync(targetModel, systemPrompt, userPrompt, null, opts, ct);
        return result.NarrativeText ?? string.Empty;
    }

    protected override async Task<LLMToolCallResult> CompleteWithToolsAsyncCore(string systemPrompt, string userPrompt, IEnumerable<ToolDefinition> tools, LLMOptions opts, CancellationToken ct)
    {
        var targetModel = string.IsNullOrEmpty(opts.Model) ? model : opts.Model;
        return await SendRequestAsync(targetModel, systemPrompt, userPrompt, tools.ToList(), opts, ct);
    }

    private async Task<LLMToolCallResult> SendRequestAsync(string targetModel, string systemPrompt, string userPrompt, List<ToolDefinition>? tools, LLMOptions opts, CancellationToken ct)
    {
        var url = $"{BaseUrl}/{targetModel}:generateContent";

        var generationConfig = new Dictionary<string, object>
        {
            ["temperature"] = opts.Temperature,
            ["maxOutputTokens"] = opts.MaxTokens,
            ["topP"] = opts.TopP
        };

        var thinkingBudget = GetThinkingBudget(opts.ReasoningEffort);
        if (thinkingBudget > 0)
            generationConfig["thinkingConfig"] = new { thinkingBudget };
        if (opts.JsonMode)
        {
            generationConfig["responseMimeType"] = "application/json";
            if (opts.JsonSchema.HasValue)
                generationConfig["responseSchema"] = opts.JsonSchema.Value;
        }

        var bodyObj = new Dictionary<string, object>
        {
            ["contents"] = new[]
            {
                new
                {
                    role = "user",
                    parts = new[] { new { text = userPrompt } }
                }
            },
            ["systemInstruction"] = new
            {
                parts = new[] { new { text = systemPrompt } }
            },
            ["generationConfig"] = generationConfig
        };

        if (tools is { Count: > 0 })
        {
            var functionDeclarations = tools.Select(t => new
            {
                name = t.Name,
                description = t.Description,
                parameters = t.Parameters
            });
            bodyObj["tools"] = new[] { new { functionDeclarations } };
        }

        var json = JsonSerializer.Serialize(bodyObj);
        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Add("x-goog-api-key", apiKey);
        req.Content = new StringContent(json, Encoding.UTF8, "application/json");

        using var res = await httpClient.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();

        var responseBody = await res.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(responseBody);
        var root = doc.RootElement;

        ExtractUsage(root);

        // Gemini returns HTTP 200 with no "candidates" when a prompt is safety-blocked —
        // routine for combat and horror content in a TTRPG — and a candidate can also come
        // back with no "parts" when it hits MAX_TOKENS. Both used to throw
        // KeyNotFoundException out of the provider.
        if (!root.TryGetProperty("candidates", out var candidates) ||
            candidates.ValueKind != JsonValueKind.Array ||
            candidates.GetArrayLength() == 0)
        {
            var blockReason = root.TryGetProperty("promptFeedback", out var feedback) &&
                              feedback.TryGetProperty("blockReason", out var reason)
                ? reason.GetString()
                : null;

            throw new InvalidOperationException(blockReason is not null
                ? $"Google AI Studio blocked the prompt (reason: {blockReason})."
                : "Google AI Studio returned no candidates.");
        }

        var candidate = candidates[0];

        if (!candidate.TryGetProperty("content", out var content) ||
            !content.TryGetProperty("parts", out var parts) ||
            parts.ValueKind != JsonValueKind.Array)
        {
            var finishReason = candidate.TryGetProperty("finishReason", out var fr) ? fr.GetString() : null;

            // MAX_TOKENS with no parts means the model produced nothing usable.
            throw new InvalidOperationException(finishReason is not null
                ? $"Google AI Studio returned no content (finishReason: {finishReason})."
                : "Google AI Studio returned no content.");
        }

        string? narrativeText = null;
        var toolCalls = new List<ToolCall>();

        foreach (var part in parts.EnumerateArray())
        {
            if (part.TryGetProperty("functionCall", out var fnCall))
            {
                var name = fnCall.GetProperty("name").GetString() ?? string.Empty;
                var args = fnCall.TryGetProperty("args", out var argsEl) ? argsEl.Clone() : JsonSerializer.Deserialize<JsonElement>("{}");
                toolCalls.Add(new ToolCall(Guid.NewGuid().ToString(), name, args));
            }
            else if (part.TryGetProperty("text", out var textEl))
            {
                narrativeText = (narrativeText ?? string.Empty) + textEl.GetString();
            }
        }

        if (toolCalls.Count > 0)
            narrativeText = null;

        return new LLMToolCallResult(narrativeText, toolCalls, GetTokenUsage());
    }

    public override async Task<float[]> GetEmbeddingAsync(string text, CancellationToken ct)
    {
        var url = $"{BaseUrl}/{_embeddingModel}:embedContent";
        var body = new
        {
            model = $"models/{_embeddingModel}",
            content = new { parts = new[] { new { text } } }
        };
        var json = JsonSerializer.Serialize(body);
        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Add("x-goog-api-key", apiKey);
        req.Content = new StringContent(json, Encoding.UTF8, "application/json");

        using var res = await httpClient.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();

        var responseBody = await res.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(responseBody);
        var values = doc.RootElement.GetProperty("embedding").GetProperty("values");
        return values.EnumerateArray().Select(e => e.GetSingle()).ToArray();
    }

    public override async Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken ct)
    {
        try
        {
            var url = $"{BaseUrl}";
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Add("x-goog-api-key", apiKey);
            using var res = await httpClient.SendAsync(req, ct);
            res.EnsureSuccessStatusCode();
            var json = await res.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("models", out var models))
                return [];
            return models.EnumerateArray()
                .Select(m => m.TryGetProperty("name", out var n) ? n.GetString()?.Replace("models/", "") : null)
                .OfType<string>()
                .OrderBy(n => n)
                .ToList();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "GoogleAIStudioLLMProvider: failed to list models");
            return [];
        }
    }

    public override async Task<bool> IsAvailableAsync(CancellationToken ct)
    {
        try
        {
            var url = $"{BaseUrl}";
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Add("x-goog-api-key", apiKey);
            using var res = await httpClient.SendAsync(req, ct);
            return res.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public override async Task<ProviderStatus> GetStatusAsync(CancellationToken ct)
    {
        try
        {
            var url = $"{BaseUrl}";
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Add("x-goog-api-key", apiKey);
            using var res = await httpClient.SendAsync(req, ct);
            return new ProviderStatus(res.IsSuccessStatusCode, model, res.IsSuccessStatusCode ? null : res.ReasonPhrase);
        }
        catch (Exception ex)
        {
            return new ProviderStatus(false, null, ex.Message);
        }
    }

    private void ExtractUsage(JsonElement root)
    {
        if (!root.TryGetProperty("usageMetadata", out var usage))
            return;

        var prompt = usage.TryGetProperty("promptTokenCount", out var p) ? p.GetInt32() : 0;
        var completion = usage.TryGetProperty("candidatesTokenCount", out var c) ? c.GetInt32() : 0;
        var total = usage.TryGetProperty("totalTokenCount", out var t) ? t.GetInt32() : prompt + completion;
        UpdateTokenUsage(new TokenUsage(prompt, completion, total));
    }
}
