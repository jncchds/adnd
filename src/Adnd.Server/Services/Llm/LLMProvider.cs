using System.Text.Json;

namespace Adnd.Server.Services.Llm;

public record TokenUsage(int PromptTokens, int CompletionTokens, int TotalTokens);

public record LLMOptions
{
    public string Model { get; init; } = string.Empty;
    public float Temperature { get; init; } = 0.7f;
    public int MaxTokens { get; init; } = 2048;
    public float TopP { get; init; } = 0.9f;
    public float FrequencyPenalty { get; init; }
    public float PresencePenalty { get; init; }
    public bool Stream { get; init; }
    public bool JsonMode { get; init; }
    public JsonElement? JsonSchema { get; init; }
    public int? TimeoutMs { get; init; }
    public string ReasoningEffort { get; init; } = "none";
    public Dictionary<string, object> ExtraParams { get; init; } = new();
}

public record ToolDefinition(string Name, string Description, JsonElement Parameters);

public record ToolCall(string Id, string Name, JsonElement Arguments);

public record LLMToolCallResult(string? NarrativeText, List<ToolCall> ToolCalls, TokenUsage Usage, string? Reasoning = null);

// Reasoning is the model's separate "thinking" output where the provider exposes it
// (Ollama's Thinking field, OpenAI-compatible gateways' reasoning_content, Gemini's
// thought-flagged parts) — null when the provider/API doesn't surface it.
public record LLMCompletionResult(string Text, string? Reasoning);

public record ProviderStatus(bool IsAvailable, string? ModelName, string? Error);

public interface ILLMProvider
{
    string ProviderId { get; }
    string EndpointUrl { get; }
    TokenUsage GetTokenUsage();
    Task<LLMCompletionResult> CompleteAsync(string systemPrompt, string userPrompt, LLMOptions opts, CancellationToken ct);
    Task<LLMToolCallResult> CompleteWithToolsAsync(string systemPrompt, string userPrompt, IEnumerable<ToolDefinition> tools, LLMOptions opts, CancellationToken ct);
    Task<float[]> GetEmbeddingAsync(string text, CancellationToken ct);
    Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken ct);
    Task<bool> IsAvailableAsync(CancellationToken ct);
    Task<ProviderStatus> GetStatusAsync(CancellationToken ct);
}

public abstract class BaseLLMProvider : ILLMProvider
{
    private TokenUsage _tokenUsage = new(0, 0, 0);

    public abstract string ProviderId { get; }
    public abstract string EndpointUrl { get; }

    public TokenUsage GetTokenUsage() => _tokenUsage;

    protected void UpdateTokenUsage(TokenUsage usage) => _tokenUsage = usage;

    public async Task<LLMCompletionResult> CompleteAsync(string systemPrompt, string userPrompt, LLMOptions opts, CancellationToken ct)
        => await CompleteAsyncCore(systemPrompt, userPrompt, opts, ct);

    protected abstract Task<LLMCompletionResult> CompleteAsyncCore(string systemPrompt, string userPrompt, LLMOptions opts, CancellationToken ct);

    public async Task<LLMToolCallResult> CompleteWithToolsAsync(string systemPrompt, string userPrompt, IEnumerable<ToolDefinition> tools, LLMOptions opts, CancellationToken ct)
    {
        try
        {
            return await CompleteWithToolsAsyncCore(systemPrompt, userPrompt, tools, opts, ct);
        }
        catch (NotSupportedException)
        {
            return await CompleteWithToolsAsyncFallback(systemPrompt, userPrompt, tools, opts, ct);
        }
    }

    protected virtual Task<LLMToolCallResult> CompleteWithToolsAsyncCore(string systemPrompt, string userPrompt, IEnumerable<ToolDefinition> tools, LLMOptions opts, CancellationToken ct)
        => throw new NotSupportedException($"{GetType().Name} does not natively support tool calling.");

    private async Task<LLMToolCallResult> CompleteWithToolsAsyncFallback(string systemPrompt, string userPrompt, IEnumerable<ToolDefinition> tools, LLMOptions opts, CancellationToken ct)
    {
        var toolList = tools.ToList();
        var toolJson = JsonSerializer.Serialize(toolList.Select(t => new
        {
            name = t.Name,
            description = t.Description,
            parameters = t.Parameters
        }));

        var augmentedSystem = $"{systemPrompt}\n\nAvailable tools (respond with JSON array of tool calls or plain text if no tool needed):\n{toolJson}\n\nTo call tools respond with a JSON array: [{{\"id\":\"call_1\",\"name\":\"tool_name\",\"arguments\":{{}}}}]\nIf no tool call is needed, respond with plain text only.";

        var response = await CompleteAsyncCore(augmentedSystem, userPrompt, opts with { JsonMode = true }, ct);

        var toolCalls = ParseToolCallsFromResponse(response.Text);
        var narrativeText = toolCalls.Count > 0 ? null : response.Text;

        return new LLMToolCallResult(narrativeText, toolCalls, _tokenUsage, response.Reasoning);
    }

    private static List<ToolCall> ParseToolCallsFromResponse(string response)
    {
        // Restrict wrapped arrays to tool-call property names so an unrelated array in a
        // JSON narrative response isn't mistaken for a list of tool calls.
        if (!JsonExtract.TryExtractArray(response, out var array, "toolCalls", "tool_calls", "tools", "calls"))
            return [];

        var result = new List<ToolCall>();
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;

            var id = item.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? Guid.NewGuid().ToString() : Guid.NewGuid().ToString();
            var name = item.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? string.Empty : string.Empty;
            var arguments = item.TryGetProperty("arguments", out var argsEl) ? argsEl.Clone() : JsonSerializer.Deserialize<JsonElement>("{}");

            if (string.IsNullOrEmpty(name))
                continue;

            result.Add(new ToolCall(id, name, arguments));
        }
        return result;
    }

    public abstract Task<float[]> GetEmbeddingAsync(string text, CancellationToken ct);
    public virtual Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken ct)
        => Task.FromResult<IReadOnlyList<string>>([]);
    public abstract Task<bool> IsAvailableAsync(CancellationToken ct);
    public abstract Task<ProviderStatus> GetStatusAsync(CancellationToken ct);
}
