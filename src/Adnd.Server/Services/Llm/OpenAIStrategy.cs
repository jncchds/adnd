using System.Text;
using System.Text.Json;

namespace Adnd.Server.Services.Llm;

/// <summary>
/// Formats requests for the OpenAI /v1/chat/completions endpoint with standard tools array.
/// </summary>
public sealed class OpenAIStrategy : IAdndLlmStrategy
{
    public string ProviderType => "openai";

    public HttpRequestMessage BuildCompletionRequest(
        string endpoint,
        string model,
        string systemPrompt,
        string userPrompt,
        LLMOptions opts)
    {
        var body = new
        {
            model = string.IsNullOrEmpty(opts.Model) ? model : opts.Model,
            stream = false,
            max_tokens = opts.MaxTokens,
            temperature = opts.Temperature,
            top_p = opts.TopP,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            }
        };

        var url = endpoint.TrimEnd('/') + "/v1/chat/completions";
        var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        return req;
    }

    public HttpRequestMessage BuildToolCallRequest(
        string endpoint,
        string model,
        string systemPrompt,
        string userPrompt,
        IEnumerable<ToolDefinition> tools,
        LLMOptions opts)
    {
        var toolDefs = tools.Select(t => new
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
            model = string.IsNullOrEmpty(opts.Model) ? model : opts.Model,
            stream = false,
            max_tokens = opts.MaxTokens,
            temperature = opts.Temperature,
            top_p = opts.TopP,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            tools = toolDefs
        };

        var url = endpoint.TrimEnd('/') + "/v1/chat/completions";
        var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        return req;
    }

    public string ParseCompletionResponse(string responseBody)
    {
        using var doc = JsonDocument.Parse(responseBody);
        return doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? string.Empty;
    }

    public LLMToolCallResult ParseToolCallResponse(string responseBody)
    {
        using var doc = JsonDocument.Parse(responseBody);
        var root = doc.RootElement;
        var message = root.GetProperty("choices")[0].GetProperty("message");

        var toolCalls = new List<ToolCall>();
        string? narrativeText = null;

        if (message.TryGetProperty("tool_calls", out var tcEl) && tcEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var tc in tcEl.EnumerateArray())
            {
                var id = tc.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? Guid.NewGuid().ToString() : Guid.NewGuid().ToString();
                var fn = tc.GetProperty("function");
                var name = fn.GetProperty("name").GetString() ?? string.Empty;
                var argsStr = fn.TryGetProperty("arguments", out var argsEl) ? argsEl.GetString() ?? "{}" : "{}";
                var args = JsonSerializer.Deserialize<JsonElement>(argsStr);
                if (!string.IsNullOrEmpty(name))
                    toolCalls.Add(new ToolCall(id, name, args));
            }
        }
        else if (message.TryGetProperty("content", out var contentEl))
        {
            narrativeText = contentEl.GetString();
        }

        ExtractUsage(root, out var usage);
        return new LLMToolCallResult(narrativeText, toolCalls, usage);
    }

    private static void ExtractUsage(JsonElement root, out TokenUsage usage)
    {
        if (root.TryGetProperty("usage", out var u))
        {
            var prompt = u.TryGetProperty("prompt_tokens", out var p) ? p.GetInt32() : 0;
            var completion = u.TryGetProperty("completion_tokens", out var c) ? c.GetInt32() : 0;
            var total = u.TryGetProperty("total_tokens", out var t) ? t.GetInt32() : prompt + completion;
            usage = new TokenUsage(prompt, completion, total);
        }
        else
        {
            usage = new TokenUsage(0, 0, 0);
        }
    }
}
