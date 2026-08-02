using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Adnd.Server.Services.Llm;

public class OpenAICompatibleLLMProvider(
    string endpointUrl,
    string? apiKey,
    string? embeddingEndpointUrl,
    string? embeddingModel,
    HttpClient httpClient,
    ILogger<OpenAICompatibleLLMProvider> logger) : BaseLLMProvider
{
    public override string ProviderId => "openaicompatible";
    public override string EndpointUrl => endpointUrl;

    protected override async Task<string> CompleteAsyncCore(string systemPrompt, string userPrompt, LLMOptions opts, CancellationToken ct)
    {
        var body = BuildBody(opts, new[]
        {
            new { role = "system", content = systemPrompt },
            new { role = "user", content = userPrompt }
        });

        var response = await PostJsonAsync($"{endpointUrl}/chat/completions", body, ct);
        using var doc = JsonDocument.Parse(response);
        var root = doc.RootElement;

        ExtractUsage(root);

        return RequireFirstChoice(root)
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? string.Empty;
    }

    /// <summary>
    /// Several OpenAI-compatible gateways (LiteLLM, vLLM, LM Studio) answer 200 OK with an
    /// {"error": ...} body, which sails past EnsureSuccessStatusCode. Reading choices[0]
    /// blindly then threw an opaque KeyNotFoundException instead of the server's message.
    /// </summary>
    private static JsonElement RequireFirstChoice(JsonElement root)
    {
        if (root.TryGetProperty("error", out var error))
        {
            var message = error.ValueKind == JsonValueKind.Object && error.TryGetProperty("message", out var m)
                ? m.GetString()
                : error.ToString();
            throw new InvalidOperationException($"LLM provider returned an error: {message}");
        }

        if (!root.TryGetProperty("choices", out var choices) ||
            choices.ValueKind != JsonValueKind.Array ||
            choices.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("LLM provider returned no choices.");
        }

        return choices[0];
    }

    protected override async Task<LLMToolCallResult> CompleteWithToolsAsyncCore(string systemPrompt, string userPrompt, IEnumerable<ToolDefinition> tools, LLMOptions opts, CancellationToken ct)
    {
        var toolList = tools.ToList();
        var toolsJson = toolList.Select(t => new
        {
            type = "function",
            function = new
            {
                name = t.Name,
                description = t.Description,
                parameters = t.Parameters
            }
        });

        var base_ = BuildBody(opts, new[]
        {
            new { role = "system", content = systemPrompt },
            new { role = "user", content = userPrompt }
        });
        base_["tools"] = toolsJson;
        var body = base_;

        var response = await PostJsonAsync($"{endpointUrl}/chat/completions", body, ct);
        using var doc = JsonDocument.Parse(response);
        var root = doc.RootElement;

        ExtractUsage(root);

        var message = RequireFirstChoice(root).GetProperty("message");

        string? narrativeText = null;
        var toolCalls = new List<ToolCall>();

        if (message.TryGetProperty("tool_calls", out var toolCallsEl) && toolCallsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var tc in toolCallsEl.EnumerateArray())
            {
                var id = tc.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? Guid.NewGuid().ToString() : Guid.NewGuid().ToString();
                var fn = tc.GetProperty("function");
                var name = fn.GetProperty("name").GetString() ?? string.Empty;
                var argsStr = fn.TryGetProperty("arguments", out var argsEl) ? argsEl.GetString() ?? "{}" : "{}";
                var args = JsonSerializer.Deserialize<JsonElement>(argsStr);
                toolCalls.Add(new ToolCall(id, name, args));
            }
        }
        else if (message.TryGetProperty("content", out var contentEl))
        {
            narrativeText = contentEl.GetString();
        }

        return new LLMToolCallResult(narrativeText, toolCalls, GetTokenUsage());
    }

    public override async Task<float[]> GetEmbeddingAsync(string text, CancellationToken ct)
    {
        // An empty model string is rejected with a 400 by most servers, and EmbeddingService
        // swallowed that into an empty vector — silently disabling RAG with no visible error.
        if (string.IsNullOrWhiteSpace(embeddingModel))
            throw new InvalidOperationException(
                "No embedding model configured for this preset. Set EmbeddingModel to enable RAG.");

        var baseUrl = embeddingEndpointUrl ?? endpointUrl;
        var body = new { input = text, model = embeddingModel };
        var response = await PostJsonAsync($"{baseUrl}/embeddings", body, ct);
        using var doc = JsonDocument.Parse(response);
        var data = doc.RootElement.GetProperty("data")[0].GetProperty("embedding");
        return data.EnumerateArray().Select(e => e.GetSingle()).ToArray();
    }

    public override async Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken ct)
    {
        try
        {
            using var req = BuildRequest(HttpMethod.Get, $"{endpointUrl}/models");
            using var res = await httpClient.SendAsync(req, ct);
            res.EnsureSuccessStatusCode();
            var json = await res.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("data", out var data))
                return [];
            return data.EnumerateArray()
                .Select(m => m.TryGetProperty("id", out var id) ? id.GetString() : null)
                .OfType<string>()
                .OrderBy(n => n)
                .ToList();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "OpenAICompatibleLLMProvider: failed to list models from {EndpointUrl}", endpointUrl);
            return [];
        }
    }

    public override async Task<bool> IsAvailableAsync(CancellationToken ct)
    {
        try
        {
            using var req = BuildRequest(HttpMethod.Get, $"{endpointUrl}/models");
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
            using var req = BuildRequest(HttpMethod.Get, $"{endpointUrl}/models");
            using var res = await httpClient.SendAsync(req, ct);
            return new ProviderStatus(res.IsSuccessStatusCode, null, res.IsSuccessStatusCode ? null : res.ReasonPhrase);
        }
        catch (Exception ex)
        {
            return new ProviderStatus(false, null, ex.Message);
        }
    }

    // Empty schema = any valid JSON; used when caller doesn't supply a specific schema
    private static readonly JsonElement _fallbackSchema =
        JsonSerializer.Deserialize<JsonElement>("{}");

    private Dictionary<string, object> BuildBody(LLMOptions opts, object messages)
    {
        var body = new Dictionary<string, object>
        {
            ["model"] = opts.Model,
            ["messages"] = messages,
            ["temperature"] = opts.Temperature,
            ["max_tokens"] = opts.MaxTokens,
            ["top_p"] = opts.TopP,
            ["stream"] = false
        };
        if (opts.JsonMode)
        {
            var schema = opts.JsonSchema ?? _fallbackSchema;
            body["response_format"] = new
            {
                type = "json_schema",
                json_schema = new { name = "response", strict = false, schema }
            };
        }
        return body;
    }

    private async Task<string> PostJsonAsync(string url, object body, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(body);
        using var req = BuildRequest(HttpMethod.Post, url);
        req.Content = new StringContent(json, Encoding.UTF8, "application/json");
        using var res = await httpClient.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadAsStringAsync(ct);
    }

    private HttpRequestMessage BuildRequest(HttpMethod method, string url)
    {
        var req = new HttpRequestMessage(method, url);
        if (!string.IsNullOrEmpty(apiKey))
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        return req;
    }

    private void ExtractUsage(JsonElement root)
    {
        if (!root.TryGetProperty("usage", out var usage))
            return;

        var prompt = usage.TryGetProperty("prompt_tokens", out var p) ? p.GetInt32() : 0;
        var completion = usage.TryGetProperty("completion_tokens", out var c) ? c.GetInt32() : 0;
        var total = usage.TryGetProperty("total_tokens", out var t) ? t.GetInt32() : prompt + completion;
        UpdateTokenUsage(new TokenUsage(prompt, completion, total));
    }
}
