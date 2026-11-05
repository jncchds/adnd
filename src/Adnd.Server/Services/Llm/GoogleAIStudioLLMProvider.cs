using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace Adnd.Server.Services.Llm;

/// <summary>
/// Google AI Studio through Gemini's own API (<c>models/{model}:generateContent</c>). Thought summaries come back
/// as reasoning, the preset's reasoning effort sets the thinking budget, structured output uses
/// <c>responseJsonSchema</c>, a reply Gemini blocks or stops says why, a busy Gemini is asked again, and errors
/// carry Google's own message instead of a bare status code.
/// </summary>
public class GoogleAIStudioLLMProvider(
    string apiKey,
    string model,
    string? endpointUrl,
    string? embeddingModel,
    string? reasoningEffort,
    HttpClient httpClient,
    ILogger<GoogleAIStudioLLMProvider> logger) : BaseLLMProvider
{
    internal const string DefaultEndpoint = "https://generativelanguage.googleapis.com/v1beta";
    private const string DefaultEmbeddingModel = "text-embedding-004";

    private readonly string _baseUrl = BaseUrl(endpointUrl);

    // The preset's embedding model was ignored entirely and this was pinned.
    private readonly string _embeddingModel =
        string.IsNullOrWhiteSpace(embeddingModel) ? DefaultEmbeddingModel : ModelId(embeddingModel);

    public override string ProviderId => "google";
    public override string EndpointUrl => _baseUrl;

    /// <summary>
    /// The API's base URL. An endpoint saved for Google's OpenAI-compatible layer (<c>…/v1beta/openai</c>) is read
    /// as the base API; a blank one is Google's. The preset's endpoint used to be ignored altogether.
    /// </summary>
    internal static string BaseUrl(string? endpoint)
    {
        var url = string.IsNullOrWhiteSpace(endpoint) ? DefaultEndpoint : endpoint.Trim().TrimEnd('/');
        if (url.EndsWith("/openai", StringComparison.OrdinalIgnoreCase)) url = url[..^"/openai".Length];
        // An endpoint copied with the models path on it.
        if (url.EndsWith("/models", StringComparison.OrdinalIgnoreCase)) url = url[..^"/models".Length];
        return url;
    }

    internal static string ModelId(string m) => m.StartsWith("models/", StringComparison.Ordinal) ? m["models/".Length..] : m;

    /// <summary>Gemini models that think: 2.5 and later (not 1.x, 2.0 or Gemma).</summary>
    internal static bool Thinks(string m)
    {
        var id = ModelId(m).ToLowerInvariant();
        return id.StartsWith("gemini-", StringComparison.Ordinal)
               && !id.StartsWith("gemini-1", StringComparison.Ordinal)
               && !id.StartsWith("gemini-2.0", StringComparison.Ordinal);
    }

    /// <summary>
    /// The thinking budget for a reasoning effort: blank = the model's own (dynamic) choice; "none" turns thinking
    /// off (Pro models cannot, so they get the smallest budget). Null when nothing is set. The preset's setting
    /// never reached this provider before — every call ran with thinking at the model's default.
    /// </summary>
    internal static int? ThinkingBudget(string m, string? effort) => effort?.Trim().ToLowerInvariant() switch
    {
        null or "" => null,
        "none" => ModelId(m).Contains("pro", StringComparison.OrdinalIgnoreCase) ? 128 : 0,
        "low" => 1024,
        "medium" => 8192,
        _ => 24576,
    };

    /// <summary>How many times a request Gemini was too busy for is tried again.</summary>
    internal const int BusyRetries = 3;

    /// <summary>Gemini is overloaded or rate-limiting ("high demand" is common on the free tier) — worth trying again shortly.</summary>
    internal static bool IsBusy(HttpStatusCode status) => status is HttpStatusCode.TooManyRequests
        or HttpStatusCode.InternalServerError or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout;

    /// <summary>Wait before try number <paramref name="attempt"/> (1-based): Google's Retry-After when it gives one (at most a minute), else 2, 4, 8 s.</summary>
    internal static TimeSpan RetryDelay(int attempt, TimeSpan? retryAfter) =>
        retryAfter is { } r && r > TimeSpan.Zero
            ? (r < TimeSpan.FromMinutes(1) ? r : TimeSpan.FromMinutes(1))
            : TimeSpan.FromSeconds(Math.Pow(2, attempt));

    /// <summary>Google's error message from an error body (<c>{"error": {"message"}}</c>), or the raw text.</summary>
    internal static string ErrorMessage(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var e) && e.TryGetProperty("message", out var m))
                return m.GetString() ?? body;
        }
        catch (JsonException)
        {
            // Not JSON (a proxy's HTML page): the raw text is the message.
        }
        return body.Length > 500 ? body[..500] : body;
    }

    protected override async Task<LLMCompletionResult> CompleteAsyncCore(string systemPrompt, string userPrompt, LLMOptions opts, CancellationToken ct)
    {
        var result = await SendRequestAsync(TargetModel(opts), systemPrompt, userPrompt, null, opts, ct);
        return new LLMCompletionResult(result.NarrativeText ?? string.Empty, result.Reasoning);
    }

    protected override async Task<LLMToolCallResult> CompleteWithToolsAsyncCore(string systemPrompt, string userPrompt, IEnumerable<ToolDefinition> tools, LLMOptions opts, CancellationToken ct)
        => await SendRequestAsync(TargetModel(opts), systemPrompt, userPrompt, tools.ToList(), opts, ct);

    private string TargetModel(LLMOptions opts) => ModelId(string.IsNullOrEmpty(opts.Model) ? model : opts.Model);

    /// <summary>
    /// The generateContent body. <paramref name="thinking"/> and <paramref name="schema"/> can be left out for a
    /// model that refuses them.
    /// </summary>
    internal static JsonObject BuildRequest(
        string targetModel, string systemPrompt, string userPrompt, List<ToolDefinition>? tools, LLMOptions opts,
        string? effort, bool thinking = true, bool schema = true)
    {
        var body = new JsonObject
        {
            ["contents"] = new JsonArray(new JsonObject
            {
                ["role"] = "user",
                ["parts"] = new JsonArray(new JsonObject { ["text"] = userPrompt }),
            }),
        };
        if (!string.IsNullOrWhiteSpace(systemPrompt))
            body["systemInstruction"] = new JsonObject { ["parts"] = new JsonArray(new JsonObject { ["text"] = systemPrompt }) };

        var config = new JsonObject();
        if (opts.Temperature > 0) config["temperature"] = opts.Temperature;
        if (opts.MaxTokens > 0) config["maxOutputTokens"] = opts.MaxTokens;
        if (opts.TopP > 0) config["topP"] = opts.TopP;
        if (opts.JsonMode)
        {
            config["responseMimeType"] = "application/json";
            // responseJsonSchema takes JSON Schema as written; responseSchema is an OpenAPI subset that refuses
            // much of what the callers send.
            if (schema && opts.JsonSchema is { } s && s.ValueKind == JsonValueKind.Object)
                config["responseJsonSchema"] = JsonNode.Parse(s.GetRawText());
        }
        if (thinking && Thinks(targetModel))
        {
            var budget = ThinkingBudget(targetModel, effort);
            var thinkingConfig = new JsonObject { ["includeThoughts"] = budget != 0 };
            if (budget is int b) thinkingConfig["thinkingBudget"] = b;
            config["thinkingConfig"] = thinkingConfig;
        }
        if (config.Count > 0) body["generationConfig"] = config;

        if (tools is { Count: > 0 })
        {
            var declarations = new JsonArray();
            foreach (var t in tools)
                declarations.Add(new JsonObject
                {
                    ["name"] = t.Name,
                    ["description"] = t.Description,
                    ["parameters"] = JsonNode.Parse(t.Parameters.GetRawText()),
                });
            body["tools"] = new JsonArray(new JsonObject { ["functionDeclarations"] = declarations });
        }
        return body;
    }

    private async Task<LLMToolCallResult> SendRequestAsync(string targetModel, string systemPrompt, string userPrompt, List<ToolDefinition>? tools, LLMOptions opts, CancellationToken ct)
    {
        var effort = string.IsNullOrWhiteSpace(reasoningEffort) ? opts.ReasoningEffort : reasoningEffort;
        var responseBody = await PostAsync(targetModel, systemPrompt, userPrompt, tools, opts, effort, ct);
        using var doc = JsonDocument.Parse(responseBody);
        var result = ParseResponse(doc.RootElement, out var usage);
        if (usage is not null) UpdateTokenUsage(usage);
        return result;
    }

    /// <summary>
    /// Sends the request. When Gemini is busy (429, 500, 503, 504) it waits and tries again, up to
    /// <see cref="BusyRetries"/> times; when it refuses the thinking settings or the schema (an older model, a
    /// schema it can't take), it is asked again without them — still for JSON.
    /// </summary>
    private async Task<string> PostAsync(
        string targetModel, string systemPrompt, string userPrompt, List<ToolDefinition>? tools, LLMOptions opts,
        string? effort, CancellationToken ct)
    {
        var url = $"{_baseUrl}/models/{Uri.EscapeDataString(targetModel)}:generateContent";
        bool thinking = true, schema = true;
        var busy = 0;
        while (true)
        {
            var body = BuildRequest(targetModel, systemPrompt, userPrompt, tools, opts, effort, thinking, schema);
            using var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
            };
            req.Headers.Add("x-goog-api-key", apiKey);
            using var res = await httpClient.SendAsync(req, ct);
            var text = await res.Content.ReadAsStringAsync(ct);
            if (res.IsSuccessStatusCode) return text;

            var error = ErrorMessage(text);
            var status = res.StatusCode;
            if (IsBusy(status) && busy < BusyRetries)
            {
                var wait = RetryDelay(++busy, res.Headers.RetryAfter?.Delta);
                logger.LogWarning("Google AI Studio is busy ({Status}: {Error}); trying again in {Seconds}s", (int)status, error, wait.TotalSeconds);
                await Task.Delay(wait, ct);
            }
            else if (status == HttpStatusCode.BadRequest && thinking && body["generationConfig"]?["thinkingConfig"] is not null
                     && error.Contains("think", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning("Google AI Studio refused the thinking settings for {Model}; asking without them", targetModel);
                thinking = false;
            }
            else if (status == HttpStatusCode.BadRequest && schema && body["generationConfig"]?["responseJsonSchema"] is not null
                     && error.Contains("schema", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning("Google AI Studio refused the response schema for {Model}; asking for plain JSON", targetModel);
                schema = false;
            }
            else
            {
                throw new HttpRequestException($"Google AI Studio: {error}", null, status);
            }
        }
    }

    /// <summary>
    /// Reads a generateContent response: narration, thought summaries and function calls. Text that comes with
    /// function calls is kept — the saga posts it before the tools run; it used to be thrown away. A blocked
    /// prompt, or a reply stopped for a reason other than finishing or the token limit (safety, recitation…),
    /// fails with Gemini's reason.
    /// </summary>
    internal static LLMToolCallResult ParseResponse(JsonElement root, out TokenUsage? usage)
    {
        usage = ReadUsage(root);

        if (root.TryGetProperty("promptFeedback", out var feedback)
            && feedback.TryGetProperty("blockReason", out var block) && block.GetString() is { Length: > 0 } blockReason)
            throw new InvalidOperationException($"Google AI Studio blocked the prompt ({blockReason}).");

        if (!root.TryGetProperty("candidates", out var candidates) ||
            candidates.ValueKind != JsonValueKind.Array ||
            candidates.GetArrayLength() == 0)
            throw new InvalidOperationException("Google AI Studio returned no candidates.");

        var candidate = candidates[0];
        var finish = candidate.TryGetProperty("finishReason", out var fr) ? fr.GetString() : null;
        if (finish is not (null or "" or "STOP" or "MAX_TOKENS" or "FINISH_REASON_UNSPECIFIED"))
            throw new InvalidOperationException($"Google AI Studio stopped the reply ({finish}).");

        StringBuilder? narrative = null, reasoning = null;
        var toolCalls = new List<ToolCall>();
        if (candidate.TryGetProperty("content", out var content) &&
            content.TryGetProperty("parts", out var parts) &&
            parts.ValueKind == JsonValueKind.Array)
        {
            foreach (var part in parts.EnumerateArray())
            {
                if (part.TryGetProperty("functionCall", out var fnCall))
                {
                    var name = fnCall.TryGetProperty("name", out var n) ? n.GetString() ?? string.Empty : string.Empty;
                    if (name.Length == 0) continue;
                    var args = fnCall.TryGetProperty("args", out var argsEl) && argsEl.ValueKind == JsonValueKind.Object
                        ? argsEl.Clone()
                        : JsonSerializer.SerializeToElement(new { });
                    toolCalls.Add(new ToolCall(Guid.NewGuid().ToString(), name, args));
                }
                else if (part.TryGetProperty("text", out var textEl) && textEl.GetString() is { Length: > 0 } text)
                {
                    var thought = part.TryGetProperty("thought", out var t) && t.ValueKind == JsonValueKind.True;
                    (thought ? reasoning ??= new() : narrative ??= new()).Append(text);
                }
            }
        }

        // MAX_TOKENS with nothing usable: say so, rather than hand back a blank turn.
        if (narrative is null && toolCalls.Count == 0 && finish == "MAX_TOKENS")
            throw new InvalidOperationException("Google AI Studio ran out of tokens before replying (finishReason: MAX_TOKENS).");

        return new LLMToolCallResult(narrative?.ToString(), toolCalls, usage ?? new TokenUsage(0, 0, 0), reasoning?.ToString());
    }

    private static TokenUsage? ReadUsage(JsonElement root)
    {
        if (!root.TryGetProperty("usageMetadata", out var u)) return null;
        var prompt = u.TryGetProperty("promptTokenCount", out var p) && p.TryGetInt32(out var pv) ? pv : 0;
        var completion = u.TryGetProperty("candidatesTokenCount", out var c) && c.TryGetInt32(out var cv) ? cv : 0;
        var thoughts = u.TryGetProperty("thoughtsTokenCount", out var th) && th.TryGetInt32(out var tv) ? tv : 0;
        var total = u.TryGetProperty("totalTokenCount", out var t) && t.TryGetInt32(out var tt) ? tt : prompt + completion + thoughts;
        return new TokenUsage(prompt, completion + thoughts, total);
    }

    public override async Task<float[]> GetEmbeddingAsync(string text, CancellationToken ct)
    {
        var url = $"{_baseUrl}/models/{Uri.EscapeDataString(_embeddingModel)}:embedContent";
        var body = new
        {
            model = $"models/{_embeddingModel}",
            content = new { parts = new[] { new { text } } }
        };
        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };
        req.Headers.Add("x-goog-api-key", apiKey);

        using var res = await httpClient.SendAsync(req, ct);
        var responseBody = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new HttpRequestException($"Google AI Studio: {ErrorMessage(responseBody)}", null, res.StatusCode);

        using var doc = JsonDocument.Parse(responseBody);
        var values = doc.RootElement.GetProperty("embedding").GetProperty("values");
        return values.EnumerateArray().Select(e => e.GetSingle()).ToArray();
    }

    /// <summary>The models list. Google pages it at 50 by default, which cut the list off part-way.</summary>
    public override async Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken ct)
    {
        try
        {
            using var res = await GetModelsAsync(ct);
            var json = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
            {
                logger.LogWarning("GoogleAIStudioLLMProvider: failed to list models: {Error}", ErrorMessage(json));
                return [];
            }
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("models", out var models))
                return [];
            return models.EnumerateArray()
                .Select(m => m.TryGetProperty("name", out var n) ? n.GetString() : null)
                .OfType<string>()
                .Select(ModelId)
                .OrderBy(n => n)
                .ToList();
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            logger.LogWarning(ex, "GoogleAIStudioLLMProvider: failed to list models");
            return [];
        }
    }

    private async Task<HttpResponseMessage> GetModelsAsync(CancellationToken ct)
    {
        // The key goes in a header, never the URL, so it stays out of logs.
        using var req = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}/models?pageSize=1000");
        req.Headers.Add("x-goog-api-key", apiKey);
        return await httpClient.SendAsync(req, ct);
    }

    public override async Task<bool> IsAvailableAsync(CancellationToken ct)
    {
        try
        {
            using var res = await GetModelsAsync(ct);
            return res.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogDebug(ex, "GoogleAIStudioLLMProvider: not reachable");
            return false;
        }
    }

    public override async Task<ProviderStatus> GetStatusAsync(CancellationToken ct)
    {
        try
        {
            using var res = await GetModelsAsync(ct);
            if (res.IsSuccessStatusCode) return new ProviderStatus(true, model, null);
            return new ProviderStatus(false, null, ErrorMessage(await res.Content.ReadAsStringAsync(ct)));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new ProviderStatus(false, null, ex.Message);
        }
    }
}
