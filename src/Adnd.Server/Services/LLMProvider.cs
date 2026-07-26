using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Adnd.Server.Models;

namespace Adnd.Server.Services;

/// <summary>
/// Pluggable LLM provider interface for game master assistance.
/// Supports multiple backends (OpenAI, Anthropic, Ollama, local models).
/// </summary>
public interface ILLMProvider
{
    /// <summary>
    /// Get the provider identifier (e.g., "openai", "anthropic", "ollama").
    /// </summary>
    string ProviderId { get; }

    /// <summary>
    /// Get the API endpoint URL used by this provider.
    /// </summary>
    string EndpointUrl { get; }

    /// <summary>
    /// Extract token usage from a raw response string (JSON-parsed from the provider).
    /// Returns null if the response is not valid JSON or contains no usage data.
    /// </summary>
    (int promptTokens, int completionTokens, int totalTokens)? GetTokenUsage(string responseText);

    /// <summary>
    /// Generate text completion with optional system prompt.
    /// </summary>
    Task<string> CompleteAsync(string systemPrompt, string userPrompt, LLMOptions? options = null);

    /// <summary>
    /// Generate text in a structured format (JSON).
    /// </summary>
    Task<T> CompleteStructuredAsync<T>(string systemPrompt, string userPrompt, LLMOptions? options = null);

    /// <summary>
    /// Generate text completion with tool/function calling support.
    /// Returns the full response including tool calls if the model wants to use them.
    /// </summary>
    Task<LLMCompletionResult> CompleteWithToolsAsync(
        string systemPrompt, string userPrompt, IEnumerable<GMToolDefinition> tools, LLMOptions? options = null);

    /// <summary>
    /// Generate an embedding vector for the given text.
    /// </summary>
    Task<float[]> GetEmbeddingAsync(string text);

    /// <summary>
    /// Check if the provider is available (connection test).
    /// </summary>
    Task<bool> IsAvailableAsync();

    /// <summary>
    /// Get provider status information.
    /// </summary>
    Task<ProviderStatus> GetStatusAsync();
}

public class LLMOptions
{
    public string? Model { get; set; }
    public float Temperature { get; set; } = 0.7f;
    public int MaxTokens { get; set; } = 2048;
    public float TopP { get; set; } = 0.9f;
    public float? FrequencyPenalty { get; set; }
    public float? PresencePenalty { get; set; }
    public Dictionary<string, object>? ExtraParams { get; set; }
    /// <summary>
    /// If set, forces the LLM to return JSON using the provider-specific mechanism.
    /// </summary>
    public JsonSchemaOutput? JsonSchemaOutput { get; set; }
}

/// <summary>
/// Describes a JSON output schema for structured LLM responses.
/// Each provider handles this differently (LM Studio uses json_schema,
/// Google uses responseMimeType, Ollama uses format:json, OpenAI uses json_schema).
/// </summary>
public class JsonSchemaOutput
{
    /// <summary>
    /// The name of the schema (used for identification/logging).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The JSON schema definition. For LM Studio, this is a full JSON schema object.
    /// For Google AI, only the type is used.
    /// </summary>
    public JsonElement Schema { get; set; }

    /// <summary>
    /// Get the schema as a serialized string.
    /// </summary>
    public string GetSchemaString() => Schema.GetRawText();
}

/// <summary>
    /// Extracts a valid JSON string from LLM output that may contain
    /// markdown code fences, reasoning text, or other non-JSON content.
    /// </summary>
    public static class JsonExtract
    {
        public static string? Extract(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var s = raw.Trim();

            // Try direct parse first (fast path)
            if (IsValidJson(s)) return s;

            // Try to find a JSON array or object in the text
            var json = ExtractEmbeddedJson(s);
            if (json != null) return json;

            return null;
        }

        private static bool IsValidJson(string s)
        {
            try
            {
                // JsonSerializer.Deserialize is stricter than JsonDocument.Parse:
                // it throws on trailing content (e.g. "[{...}] }").
                // This is exactly what we want for validation.
                JsonSerializer.Deserialize<object>(s);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string? ExtractEmbeddedJson(string s)
        {
            // First try: markdown code fences (```json ... ``` or ``` ... ```)
            var fenceMatch = System.Text.RegularExpressions.Regex.Match(s, @"```(?:json)?\s*\n?(.+?)```", System.Text.RegularExpressions.RegexOptions.Singleline);
            if (fenceMatch.Success && IsValidJson(fenceMatch.Groups[1].Value.Trim()))
                return fenceMatch.Groups[1].Value.Trim();

            // Second try: find first { or [ and parse forward
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] != '{' && s[i] != '[') continue;
                var json = TryFindClosingBracket(s, i);
                if (json != null && IsValidJson(json))
                    return json;
            }

            // Third try: try to find a valid JSON object by trimming trailing non-JSON content
            // Some LLMs return valid JSON followed by extra text like "}" or markdown
            for (int i = s.Length - 1; i >= 0; i--)
            {
                var trimmed = s[..(i + 1)];
                if (IsValidJson(trimmed))
                    return trimmed;
            }

            return null;
        }

        private static string? TryFindClosingBracket(string s, int start)
        {
            char open = s[start];
            char close = open == '{' ? '}' : ']';
            int depth = 0;
            bool inString = false;
            bool escaped = false;

            for (int i = start; i < s.Length; i++)
            {
                var c = s[i];
                if (escaped)
                {
                    escaped = false;
                    continue;
                }
                if (c == '\\') { escaped = true; continue; }
                if (c == '"') { inString = !inString; continue; }
                if (inString) continue;
                if (c == open) depth++;
                else if (c == close) { depth--; if (depth == 0) return s.Substring(start, i - start + 1); }
            }
            return null;
        }
    }

public class ProviderStatus
{
    public string ProviderId { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Result from a tool-calling LLM completion.
/// Contains the response text and any tool calls the model requested.
/// </summary>
public class LLMCompletionResult
{
    public string Content { get; set; } = string.Empty;
    public List<ToolCall> ToolCalls { get; set; } = new();
    public (int promptTokens, int completionTokens, int totalTokens)? TokenUsage { get; set; }

    public bool HasToolCalls => ToolCalls.Any();
}

/// <summary>
/// A tool call requested by the LLM.
/// </summary>
public class ToolCall
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;

    /// <summary>
    /// Deserialize the arguments to a specific type.
    /// </summary>
    public T? DeserializeArguments<T>() where T : new()
    {
        if (string.IsNullOrEmpty(Arguments)) return default;
        return JsonSerializer.Deserialize<T>(Arguments) ?? new T();
    }
}

/// <summary>
/// Base class for LLM providers with common functionality.
/// Concrete providers: OllamaLLMProvider, LmStudioLLMProvider, OpenAILLMProvider, GoogleAIStudioLLMProvider.
/// Preset-based wrappers: ProviderFromPresets.cs
/// </summary>
public abstract class BaseLLMProvider : ILLMProvider
{
    protected readonly ILogger<BaseLLMProvider> _logger;
    protected readonly IConfiguration _configuration;

    protected BaseLLMProvider(ILogger<BaseLLMProvider> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public abstract string ProviderId { get; }

    public abstract string EndpointUrl { get; }

    /// <summary>
    /// Get the model name used by this provider (for logging).
    /// </summary>
    public virtual string ModelName => "unknown";

    /// <summary>
    /// Log a provider interaction for observability.
    /// </summary>
    protected void LogProviderInteraction(
        string operation,
        string status,
        string systemPrompt,
        string userPrompt,
        string response,
        long durationMs,
        int? promptTokens = null,
        int? completionTokens = null,
        int? totalTokens = null,
        string? toolCallInfo = null,
        string? error = null)
    {
        var statusTag = status == "success" ? "[PROVIDER]" : $"[PROVIDER] {status.ToUpper()}";

        if (error != null)
        {
            _logger.LogError("{Status} {Operation} | Provider={ProviderId} | Model={Model} | Duration={Duration}ms | Error={Error}",
                statusTag, operation, ProviderId, ModelName, durationMs, error);
            return;
        }

        var systemPromptPreview = systemPrompt.Length > 100 ? systemPrompt[..100] + "..." : systemPrompt;
        var userPromptPreview = userPrompt.Length > 100 ? userPrompt[..100] + "..." : userPrompt;
        var responsePreview = response.Length > 200 ? response[..200] + "..." : response;

        var logMsg = $"{statusTag} {operation} | Provider={ProviderId} | Model={ModelName} | Duration={durationMs}ms | SystemPrompt={systemPromptPreview} | UserPrompt={userPromptPreview} | Response={responsePreview}";

        if (status == "success")
        {
            if (promptTokens.HasValue || completionTokens.HasValue)
            {
                logMsg += $" | Tokens(P:{promptTokens ?? 0},C:{completionTokens ?? 0},T:{totalTokens ?? 0})";
            }
            if (!string.IsNullOrEmpty(toolCallInfo))
            {
                logMsg += $" | Tools={toolCallInfo}";
            }
            _logger.LogInformation(logMsg);
        }
        else
        {
            _logger.LogWarning(logMsg);
        }
    }

    public abstract (int promptTokens, int completionTokens, int totalTokens)? GetTokenUsage(string responseText);

    /// <summary>
    /// Wrapper that logs the interaction and delegates to CompleteAsyncCore.
    /// Concrete providers should implement CompleteAsyncCore instead of overriding CompleteAsync.
    /// </summary>
    public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, LLMOptions? options = null)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        string? error = null;
        string? response = null;

        try
        {
            response = await CompleteAsyncCore(systemPrompt, userPrompt, options);
            sw.Stop();

            var tokenUsage = GetTokenUsage(response ?? "");
            LogProviderInteraction(
                "CompleteAsync",
                "success",
                systemPrompt,
                userPrompt,
                response ?? "",
                sw.ElapsedMilliseconds,
                tokenUsage?.promptTokens,
                tokenUsage?.completionTokens,
                tokenUsage?.totalTokens);

            return response ?? "";
        }
        catch (Exception ex)
        {
            sw.Stop();
            error = ex.Message;
            LogProviderInteraction(
                "CompleteAsync",
                "error",
                systemPrompt,
                userPrompt,
                "<exception>",
                sw.ElapsedMilliseconds,
                error: error);
            throw;
        }
    }

    /// <summary>
    /// Core implementation of text completion. Override this in concrete providers.
    /// </summary>
    protected abstract Task<string> CompleteAsyncCore(string systemPrompt, string userPrompt, LLMOptions? options = null);

    /// <summary>
    /// Calls CompleteAsync with a JSON-enforcing system prompt,
    /// strips markdown code fences if present, and deserializes the result.
    /// </summary>
    public async Task<T> CompleteStructuredAsync<T>(string systemPrompt, string userPrompt, LLMOptions? options = null)
    {
        var jsonPrompt = systemPrompt + "\n\nRespond with ONLY valid JSON. No markdown, no explanation.";
        var text = await CompleteAsync(jsonPrompt, userPrompt, options);

        var jsonText = StripMarkdownCodeFences(text.Trim());
        return JsonSerializer.Deserialize<T>(jsonText)
            ?? throw new InvalidOperationException("Failed to deserialize LLM response");
    }

    /// <summary>
    /// Strips surrounding markdown code fences (```json ... ``` or ``` ... ```).
    /// </summary>
    protected static string StripMarkdownCodeFences(string text)
    {
        if (text.StartsWith("```"))
        {
            var lines = text.Split('\n');
            return string.Join('\n', lines.Skip(1).Take(lines.Length - 2));
        }
        return text;
    }

    /// <summary>
    /// Wrapper that logs the embedding generation and delegates to GetEmbeddingAsyncCore.
    /// Concrete providers should implement GetEmbeddingAsyncCore instead of overriding GetEmbeddingAsync.
    /// </summary>
    public async Task<float[]> GetEmbeddingAsync(string text)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        string? error = null;
        float[]? result = null;

        try
        {
            result = await GetEmbeddingAsyncCore(text);
            sw.Stop();

            _logger.LogInformation("[PROVIDER] GetEmbeddingAsync | Provider={ProviderId} | Model={Model} | TextLen={TextLen} | Dim={Dim} | Duration={Duration}ms",
                ProviderId, ModelName, text.Length, result?.Length ?? 0, sw.ElapsedMilliseconds);

            return result ?? Array.Empty<float>();
        }
        catch (Exception ex)
        {
            sw.Stop();
            error = ex.Message;
            _logger.LogError("[PROVIDER] GetEmbeddingAsync | Provider={ProviderId} | Model={Model} | TextLen={TextLen} | Duration={Duration}ms | Error={Error}",
                ProviderId, ModelName, text.Length, sw.ElapsedMilliseconds, error);
            throw;
        }
    }

    /// <summary>
    /// Core implementation of embedding generation. Override this in concrete providers.
    /// </summary>
    protected abstract Task<float[]> GetEmbeddingAsyncCore(string text);

    public abstract Task<bool> IsAvailableAsync();

    public abstract Task<ProviderStatus> GetStatusAsync();

    /// <summary>
    /// Wrapper that logs the tool-calling interaction and delegates to CompleteWithToolsAsyncCore.
    /// Concrete providers should implement CompleteWithToolsAsyncCore instead of overriding CompleteWithToolsAsync.
    /// </summary>
    public async Task<LLMCompletionResult> CompleteWithToolsAsync(
        string systemPrompt, string userPrompt, IEnumerable<GMToolDefinition> tools, LLMOptions? options = null)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        string? error = null;
        LLMCompletionResult? result = null;

        try
        {
            result = await CompleteWithToolsAsyncCore(systemPrompt, userPrompt, tools, options);
            sw.Stop();

            var toolCallInfo = result.HasToolCalls
                ? $"{result.ToolCalls.Count} calls: {string.Join(", ", result.ToolCalls.Select(tc => tc.Name))}"
                : "none";
            var tokenUsage = result.TokenUsage ?? GetTokenUsage(result.Content);

            LogProviderInteraction(
                "CompleteWithToolsAsync",
                "success",
                systemPrompt,
                userPrompt,
                result.Content,
                sw.ElapsedMilliseconds,
                tokenUsage?.promptTokens,
                tokenUsage?.completionTokens,
                tokenUsage?.totalTokens,
                toolCallInfo);

            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();
            error = ex.Message;
            LogProviderInteraction(
                "CompleteWithToolsAsync",
                "error",
                systemPrompt,
                userPrompt,
                "<exception>",
                sw.ElapsedMilliseconds,
                error: error);
            throw;
        }
    }

    /// <summary>
    /// Core implementation of tool-calling completion. Override this in concrete providers.
    /// </summary>
    protected virtual Task<LLMCompletionResult> CompleteWithToolsAsyncCore(
        string systemPrompt, string userPrompt, IEnumerable<GMToolDefinition> tools, LLMOptions? options = null) =>
        CompleteWithToolsAsyncFallback(systemPrompt, userPrompt, tools, options);

    /// <summary>
    /// Default tool-calling implementation that falls back to plain text completion.
    /// Override CompleteWithToolsAsyncCore in concrete providers that support native tool calling.
    /// </summary>
    private async Task<LLMCompletionResult> CompleteWithToolsAsyncFallback(
        string systemPrompt, string userPrompt, IEnumerable<GMToolDefinition> tools, LLMOptions? options = null)
    {
        // Fallback: append tool descriptions to system prompt and ask for JSON output
        var toolDefs = tools.ToList();
        if (!toolDefs.Any())
        {
            var fallbackResult = await CompleteAsync(systemPrompt, userPrompt, options);
            return new LLMCompletionResult { Content = fallbackResult, TokenUsage = GetTokenUsage(fallbackResult) };
        }

        var toolDescriptions = string.Join("\n\n", toolDefs.Select(t =>
            $"Tool: {t.Name}\nDescription: {t.Description}\nParameters schema: {JsonSerializer.Serialize(t.Parameters)}"));

        var enhancedSystemPrompt = $"{systemPrompt}\n\n# Available Tools\n{toolDescriptions}" +
            "\n\nIf you need to use a tool, respond with a JSON array of tool calls:\n" +
            "[{\"id\": \"call_1\", \"name\": \"tool_name\", \"arguments\": {}}]\n" +
            "Otherwise respond with your narrative text.";

        var result = await CompleteAsync(enhancedSystemPrompt, userPrompt, options);

        // Try to parse tool calls from the response
        var parsed = ParseToolCallsFromResponse(result);

        return new LLMCompletionResult
        {
            Content = result,
            ToolCalls = parsed,
            TokenUsage = GetTokenUsage(result)
        };
    }

    /// <summary>
    /// Parse tool calls from the LLM response text.
    /// Looks for JSON arrays of {id, name, arguments} objects.
    /// </summary>
    protected List<ToolCall> ParseToolCallsFromResponse(string response)
    {
        // Try to find a JSON array in the response
        var trimmed = response.Trim();
        if (!trimmed.StartsWith("[")) return new List<ToolCall>();

        try
        {
            // Find the first complete JSON array
            var bracketCount = 0;
            var arrayEnd = -1;
            for (var i = 0; i < trimmed.Length; i++)
            {
                if (trimmed[i] == '[') bracketCount++;
                else if (trimmed[i] == ']')
                {
                    bracketCount--;
                    if (bracketCount == 0)
                    {
                        arrayEnd = i + 1;
                        break;
                    }
                }
            }

            if (arrayEnd < 0) return new List<ToolCall>();

            var json = trimmed[..arrayEnd];
            var calls = JsonSerializer.Deserialize<List<ToolCallRequest>>(json);
            if (calls == null || !calls.Any()) return new List<ToolCall>();

            return calls.Select(c => new ToolCall
            {
                Id = c.id ?? $"call_{Guid.NewGuid():N}"[..8],
                Name = c.name ?? "",
                Arguments = c.arguments ?? ""
            }).ToList();
        }
        catch
        {
            return new List<ToolCall>();
        }
    }

    private class ToolCallRequest
    {
        public string? id { get; set; }
        public string? name { get; set; }
        public string? arguments { get; set; }
    }
}
