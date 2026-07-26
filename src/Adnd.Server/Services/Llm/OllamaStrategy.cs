using System.Text;
using System.Text.Json;

namespace Adnd.Server.Services.Llm;

/// <summary>
/// Formats requests for the Ollama /api/chat endpoint.
/// Uses format:"json" for structured output. Tool calling uses prompt-based fallback.
/// </summary>
public sealed class OllamaStrategy : IAdndLlmStrategy
{
    public string ProviderType => "ollama";

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
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            options = new
            {
                temperature = opts.Temperature,
                top_p = opts.TopP,
                num_predict = opts.MaxTokens
            }
        };

        var url = endpoint.TrimEnd('/') + "/api/chat";
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
        var toolList = tools.ToList();
        var toolJson = JsonSerializer.Serialize(toolList.Select(t => new
        {
            name = t.Name,
            description = t.Description,
            parameters = t.Parameters
        }));

        var augmentedSystem = $"{systemPrompt}\n\nAvailable tools:\n{toolJson}\n\nRespond with a JSON array of tool calls: [{{\"id\":\"call_1\",\"name\":\"tool_name\",\"arguments\":{{}}}}] or plain text if no tool is needed.";

        var body = new
        {
            model = string.IsNullOrEmpty(opts.Model) ? model : opts.Model,
            stream = false,
            format = "json",
            messages = new[]
            {
                new { role = "system", content = augmentedSystem },
                new { role = "user", content = userPrompt }
            },
            options = new
            {
                temperature = opts.Temperature,
                top_p = opts.TopP,
                num_predict = opts.MaxTokens
            }
        };

        var url = endpoint.TrimEnd('/') + "/api/chat";
        var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        return req;
    }

    public string ParseCompletionResponse(string responseBody)
    {
        using var doc = JsonDocument.Parse(responseBody);
        var root = doc.RootElement;

        if (root.TryGetProperty("message", out var msg) &&
            msg.TryGetProperty("content", out var content))
        {
            return content.GetString() ?? string.Empty;
        }

        return string.Empty;
    }

    public LLMToolCallResult ParseToolCallResponse(string responseBody)
    {
        var text = ParseCompletionResponse(responseBody);

        if (JsonExtract.TryExtractArray(text, out var array))
        {
            var toolCalls = new List<ToolCall>();
            foreach (var item in array.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                var id = item.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? Guid.NewGuid().ToString() : Guid.NewGuid().ToString();
                var name = item.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? string.Empty : string.Empty;
                var args = item.TryGetProperty("arguments", out var argsEl) ? argsEl.Clone() : JsonSerializer.Deserialize<JsonElement>("{}");
                if (!string.IsNullOrEmpty(name))
                    toolCalls.Add(new ToolCall(id, name, args));
            }

            if (toolCalls.Count > 0)
                return new LLMToolCallResult(null, toolCalls, new TokenUsage(0, 0, 0));
        }

        return new LLMToolCallResult(text, [], new TokenUsage(0, 0, 0));
    }
}
