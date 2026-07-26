using System.Text.Json;
using Adnd.Server.Models;
using OllamaSharp;
using OllamaSharp.Models;
using OllamaSharp.Models.Chat;
using OllamaMsg = OllamaSharp.Models.Chat.Message;

namespace Adnd.Server.Services.Llm;

/// <summary>
/// Ollama strategy using OllamaSharp — supports thinking tokens and JSON schema.
/// Tool calling falls back to text-based approach (schema in system prompt) since
/// not all Ollama models support native tool calls reliably.
/// </summary>
public class OllamaStrategy : IAdndLlmStrategy
{
    public string ProviderKey => "ollama";

    public async Task<string> CompleteAsync(LlmStrategyRequest request, CancellationToken ct = default)
    {
        var client = BuildClient(request.Config);

        var thinkingEnabled = !string.IsNullOrWhiteSpace(request.Config.ReasoningEffort)
            && request.Config.ReasoningEffort != "none";

        var chatRequest = new ChatRequest
        {
            Model = request.Config.ModelName,
            Stream = false,
            Messages =
            [
                new OllamaMsg { Role = "system", Content = request.SystemPrompt },
                new OllamaMsg { Role = "user", Content = request.UserPrompt },
            ],
            Options = new RequestOptions
            {
                Temperature = request.Temperature,
                NumPredict = request.MaxTokens,
            },
        };

        if (thinkingEnabled)
            chatRequest.Think = true;

        if (request.JsonSchema != null)
            chatRequest.Format = JsonSerializer.Deserialize<JsonElement>(request.JsonSchema.GetSchemaString());

        var sb = new System.Text.StringBuilder();
        await foreach (var chunk in client.ChatAsync(chatRequest, ct))
        {
            if (chunk?.Message?.Content is { Length: > 0 } content)
                sb.Append(content);
        }
        return sb.ToString();
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

        // Text-based fallback: embed tool schemas in the system prompt
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
        var embeddingEndpoint = config.EmbeddingEndpoint ?? config.Endpoint;
        var embeddingModel = config.EmbeddingModel ?? "nomic-embed-text";

        using var http = new HttpClient { BaseAddress = new Uri(embeddingEndpoint) };
        var apiClient = new OllamaApiClient(http, embeddingModel);

        var response = await apiClient.EmbedAsync(new OllamaSharp.Models.EmbedRequest
        {
            Model = embeddingModel,
            Input = [text],
        }, ct);

        return response?.Embeddings?.FirstOrDefault()?.ToArray() ?? [];
    }

    private OllamaApiClient BuildClient(LlmStrategyConfig config)
    {
        var http = new HttpClient
        {
            BaseAddress = new Uri(config.Endpoint),
            Timeout = TimeSpan.FromMilliseconds(config.TimeoutMs ?? 120_000),
        };
        return new OllamaApiClient(http, config.ModelName);
    }

    private static List<ToolCall> ParseToolCallsFromText(string response)
    {
        var trimmed = response.Trim();
        if (!trimmed.StartsWith("[")) return [];

        try
        {
            var doc = JsonDocument.Parse(trimmed);
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
        catch
        {
            return [];
        }
    }
}
