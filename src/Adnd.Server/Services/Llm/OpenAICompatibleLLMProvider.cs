using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Adnd.Server.Services.Llm;

public class OpenAICompatibleLLMProvider(
    string endpointUrl,
    string? apiKey,
    string? embeddingEndpointUrl,
    HttpClient httpClient) : BaseLLMProvider
{
    public override string ProviderId => "openaicompatible";
    public override string EndpointUrl => endpointUrl;

    protected override async Task<string> CompleteAsyncCore(string systemPrompt, string userPrompt, LLMOptions opts, CancellationToken ct)
    {
        var body = new
        {
            model = opts.Model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            temperature = opts.Temperature,
            max_tokens = opts.MaxTokens,
            top_p = opts.TopP,
            stream = false
        };

        var response = await PostJsonAsync($"{endpointUrl}/chat/completions", body, ct);
        using var doc = JsonDocument.Parse(response);
        var root = doc.RootElement;

        ExtractUsage(root);

        return root
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? string.Empty;
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

        var body = new
        {
            model = opts.Model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            temperature = opts.Temperature,
            max_tokens = opts.MaxTokens,
            top_p = opts.TopP,
            stream = false,
            tools = toolsJson
        };

        var response = await PostJsonAsync($"{endpointUrl}/chat/completions", body, ct);
        using var doc = JsonDocument.Parse(response);
        var root = doc.RootElement;

        ExtractUsage(root);

        var message = root.GetProperty("choices")[0].GetProperty("message");

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
        var baseUrl = embeddingEndpointUrl ?? endpointUrl;
        var body = new { input = text, model = string.Empty };
        var response = await PostJsonAsync($"{baseUrl}/embeddings", body, ct);
        using var doc = JsonDocument.Parse(response);
        var data = doc.RootElement.GetProperty("data")[0].GetProperty("embedding");
        return data.EnumerateArray().Select(e => e.GetSingle()).ToArray();
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
