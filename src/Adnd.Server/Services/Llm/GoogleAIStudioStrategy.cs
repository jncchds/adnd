using System.Text;
using System.Text.Json;

namespace Adnd.Server.Services.Llm;

/// <summary>
/// Formats requests for the Google AI Studio generateContent endpoint.
/// Uses functionDeclarations for tool definitions and parses functionCall responses.
/// </summary>
public sealed class GoogleAIStudioStrategy : IAdndLlmStrategy
{
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models";

    public string ProviderType => "google";

    public HttpRequestMessage BuildCompletionRequest(
        string endpoint,
        string model,
        string systemPrompt,
        string userPrompt,
        LLMOptions opts)
    {
        // endpoint param is unused for Google — base URL is fixed
        var targetModel = string.IsNullOrEmpty(opts.Model) ? model : opts.Model;
        var apiKey = ExtractApiKeyFromEndpoint(endpoint);
        var url = $"{BaseUrl}/{targetModel}:generateContent?key={apiKey}";

        var body = new
        {
            contents = new[]
            {
                new { role = "user", parts = new[] { new { text = userPrompt } } }
            },
            systemInstruction = new { parts = new[] { new { text = systemPrompt } } },
            generationConfig = new
            {
                temperature = opts.Temperature,
                maxOutputTokens = opts.MaxTokens,
                topP = opts.TopP
            }
        };

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
        var targetModel = string.IsNullOrEmpty(opts.Model) ? model : opts.Model;
        var apiKey = ExtractApiKeyFromEndpoint(endpoint);
        var url = $"{BaseUrl}/{targetModel}:generateContent?key={apiKey}";

        var functionDeclarations = toolList.Select(t => new
        {
            name = t.Name,
            description = t.Description,
            parameters = t.Parameters
        });

        var body = new
        {
            contents = new[]
            {
                new { role = "user", parts = new[] { new { text = userPrompt } } }
            },
            systemInstruction = new { parts = new[] { new { text = systemPrompt } } },
            generationConfig = new
            {
                temperature = opts.Temperature,
                maxOutputTokens = opts.MaxTokens,
                topP = opts.TopP
            },
            tools = new[] { new { functionDeclarations } }
        };

        var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        return req;
    }

    public string ParseCompletionResponse(string responseBody)
    {
        using var doc = JsonDocument.Parse(responseBody);
        var parts = doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts");

        var sb = new StringBuilder();
        foreach (var part in parts.EnumerateArray())
        {
            if (part.TryGetProperty("text", out var textEl))
                sb.Append(textEl.GetString());
        }

        return sb.ToString();
    }

    public LLMToolCallResult ParseToolCallResponse(string responseBody)
    {
        using var doc = JsonDocument.Parse(responseBody);
        var root = doc.RootElement;

        var candidate = root.GetProperty("candidates")[0];
        var content = candidate.GetProperty("content");
        var parts = content.GetProperty("parts");

        string? narrativeText = null;
        var toolCalls = new List<ToolCall>();

        foreach (var part in parts.EnumerateArray())
        {
            if (part.TryGetProperty("functionCall", out var fnCall))
            {
                var name = fnCall.GetProperty("name").GetString() ?? string.Empty;
                var args = fnCall.TryGetProperty("args", out var argsEl)
                    ? argsEl.Clone()
                    : JsonSerializer.Deserialize<JsonElement>("{}");
                toolCalls.Add(new ToolCall(Guid.NewGuid().ToString(), name, args));
            }
            else if (part.TryGetProperty("text", out var textEl))
            {
                narrativeText = (narrativeText ?? string.Empty) + textEl.GetString();
            }
        }

        if (toolCalls.Count > 0) narrativeText = null;

        ExtractUsage(root, out var usage);
        return new LLMToolCallResult(narrativeText, toolCalls, usage);
    }

    private static string ExtractApiKeyFromEndpoint(string endpoint)
    {
        // endpoint may carry the API key as a query param (e.g. "https://...?key=xxx")
        // or may just be the base URL when the key is stored separately
        var qi = endpoint.IndexOf("key=", StringComparison.OrdinalIgnoreCase);
        if (qi < 0) return string.Empty;
        var start = qi + 4;
        var amp = endpoint.IndexOf('&', start);
        return amp < 0 ? endpoint[start..] : endpoint[start..amp];
    }

    private static void ExtractUsage(JsonElement root, out TokenUsage usage)
    {
        if (root.TryGetProperty("usageMetadata", out var u))
        {
            var prompt = u.TryGetProperty("promptTokenCount", out var p) ? p.GetInt32() : 0;
            var completion = u.TryGetProperty("candidatesTokenCount", out var c) ? c.GetInt32() : 0;
            var total = u.TryGetProperty("totalTokenCount", out var t) ? t.GetInt32() : prompt + completion;
            usage = new TokenUsage(prompt, completion, total);
        }
        else
        {
            usage = new TokenUsage(0, 0, 0);
        }
    }
}
