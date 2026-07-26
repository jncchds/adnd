using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Adnd.Server.Models;

namespace Adnd.Server.Services.Llm;

/// <summary>
/// OpenAI-compatible strategy (LM Studio, OpenRouter, vLLM, etc.).
/// Uses a raw SSE reader to capture non-standard fields like <c>reasoning_content</c>.
/// Tool calling falls back to text-based approach.
/// </summary>
public class OpenAICompatibleStrategy : IAdndLlmStrategy
{
    public string ProviderKey => "lmstudio";

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task<string> CompleteAsync(LlmStrategyRequest request, CancellationToken ct = default)
    {
        var (content, _) = await SendSseRequestAsync(request, ct);
        return content;
    }

    public async Task<LlmStrategyToolResult> CompleteWithToolsAsync(
        LlmStrategyRequest request, IEnumerable<GMToolDefinition> tools, CancellationToken ct = default)
    {
        var toolList = tools.ToList();
        if (toolList.Count == 0)
        {
            var plain = await CompleteAsync(request, ct);
            return new LlmStrategyToolResult(plain, [], null);
        }

        var toolDescriptions = string.Join("\n\n", toolList.Select(t =>
            $"Tool: {t.Name}\nDescription: {t.Description}\nParameters: {JsonSerializer.Serialize(t.Parameters)}"));

        var enhancedSystem = $"{request.SystemPrompt}\n\n# Available Tools\n{toolDescriptions}\n\n" +
            "If you need to use a tool, respond with a JSON array: " +
            "[{\"id\":\"call_1\",\"name\":\"tool_name\",\"arguments\":{}}]\n" +
            "Otherwise respond with narrative text.";

        var enhanced = request with { SystemPrompt = enhancedSystem };
        var result = await CompleteAsync(enhanced, ct);
        var parsed = ParseToolCallsFromText(result);
        return new LlmStrategyToolResult(result, parsed, null);
    }

    public async Task<float[]> GetEmbeddingAsync(string text, LlmStrategyConfig config, CancellationToken ct = default)
    {
        using var http = BuildHttpClient(config);
        var endpoint = ResolveChatUrl(config.EmbeddingEndpoint ?? config.Endpoint)
            .Replace("/chat/completions", "/embeddings", StringComparison.OrdinalIgnoreCase);

        var embeddingModel = config.EmbeddingModel ?? config.ModelName;
        var payload = new { model = embeddingModel, input = text };

        var response = await http.PostAsJsonAsync(endpoint, payload, _jsonOpts, ct);
        response.EnsureSuccessStatusCode();

        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        if (doc.RootElement.TryGetProperty("data", out var data) && data.GetArrayLength() > 0)
        {
            var embeddingArr = data[0].GetProperty("embedding");
            return embeddingArr.EnumerateArray().Select(e => e.GetSingle()).ToArray();
        }
        return [];
    }

    private async Task<(string content, string? reasoning)> SendSseRequestAsync(
        LlmStrategyRequest request, CancellationToken ct)
    {
        using var http = BuildHttpClient(request.Config);
        var chatUrl = ResolveChatUrl(request.Config.Endpoint);

        object? responseFormat = null;
        if (request.JsonSchema != null)
        {
            responseFormat = new
            {
                type = "json_schema",
                json_schema = new
                {
                    name = request.JsonSchema.Name,
                    schema = JsonSerializer.Deserialize<JsonElement>(request.JsonSchema.GetSchemaString()),
                    strict = true,
                },
            };
        }

        var requestBody = new
        {
            model = request.Config.ModelName,
            stream = true,
            messages = new[]
            {
                new { role = "system", content = request.SystemPrompt },
                new { role = "user", content = request.UserPrompt },
            },
            temperature = (float?)request.Temperature,
            max_tokens = (int?)request.MaxTokens,
            response_format = responseFormat,
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, chatUrl)
        {
            Content = JsonContent.Create(requestBody, options: _jsonOpts),
        };

        using var response = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new System.IO.StreamReader(stream);

        var contentSb = new System.Text.StringBuilder();
        var reasoningSb = new System.Text.StringBuilder();

        string? line;
        while (!ct.IsCancellationRequested && (line = await reader.ReadLineAsync(ct)) is not null)
        {
            if (!line.StartsWith("data: ", StringComparison.Ordinal)) continue;
            var json = line["data: ".Length..];
            if (json == "[DONE]") break;

            try
            {
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("choices", out var choices)) continue;
                if (choices.GetArrayLength() == 0) continue;
                var delta = choices[0].GetProperty("delta");

                if (delta.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String)
                    contentSb.Append(c.GetString());
                if (delta.TryGetProperty("reasoning_content", out var r) && r.ValueKind == JsonValueKind.String)
                    reasoningSb.Append(r.GetString());
            }
            catch (JsonException) { }
        }

        return (contentSb.ToString(), reasoningSb.Length > 0 ? reasoningSb.ToString() : null);
    }

    private HttpClient BuildHttpClient(LlmStrategyConfig config)
    {
        var http = new HttpClient
        {
            Timeout = TimeSpan.FromMilliseconds(config.TimeoutMs ?? 120_000),
        };
        if (!string.IsNullOrWhiteSpace(config.ApiKey))
            http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", config.ApiKey);
        return http;
    }

    private static string ResolveChatUrl(string endpoint)
    {
        var base_ = endpoint.TrimEnd('/');
        return base_.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase)
            ? base_
            : base_ + "/chat/completions";
    }

    private static List<ToolCall> ParseToolCallsFromText(string response)
    {
        var trimmed = response.Trim();
        if (!trimmed.StartsWith("[")) return [];

        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return [];

            return doc.RootElement.EnumerateArray()
                .Select(el => new ToolCall
                {
                    Id = el.TryGetProperty("id", out var id) ? id.GetString() ?? $"call_{Guid.NewGuid():N}"[..8] : $"call_{Guid.NewGuid():N}"[..8],
                    Name = el.TryGetProperty("name", out var name) ? name.GetString() ?? "" : "",
                    Arguments = el.TryGetProperty("arguments", out var args) ? args.GetRawText() : "{}",
                })
                .Where(tc => !string.IsNullOrEmpty(tc.Name))
                .ToList();
        }
        catch { return []; }
    }
}
