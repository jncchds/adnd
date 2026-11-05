#pragma warning disable OPENAI001 // ReasoningEffortLevel is marked experimental in the SDK.
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Embeddings;

namespace Adnd.Server.Services.Llm;

public class OpenAILLMProvider(
    string apiKey,
    string model,
    string? endpointUrl,
    string? embeddingModel,
    string? reasoningEffort,
    HttpClient httpClient,
    ILogger<OpenAILLMProvider> logger) : BaseLLMProvider
{
    public override string ProviderId => "openai";
    public override string EndpointUrl => endpointUrl ?? "https://api.openai.com/v1";

    /// <summary>
    /// The SDK's client options over the pooled HttpClient, with its timeout. The SDK made its own transport with a
    /// 100 s network timeout, so the preset's timeout never applied and a long GM step was cut off mid-reply.
    /// </summary>
    private OpenAIClientOptions Options()
    {
        var options = new OpenAIClientOptions
        {
            Transport = new HttpClientPipelineTransport(httpClient),
            NetworkTimeout = httpClient.Timeout,
        };
        if (!string.IsNullOrEmpty(endpointUrl)) options.Endpoint = new Uri(endpointUrl.TrimEnd('/'));
        return options;
    }

    private ChatClient CreateChatClient(string targetModel) =>
        new(targetModel, new ApiKeyCredential(apiKey), Options());

    private EmbeddingClient CreateEmbeddingClient(string targetModel) =>
        new(targetModel, new ApiKeyCredential(apiKey), Options());

    /// <summary>The preset's reasoning effort, which never reached this provider. "none" (the default) sends nothing.</summary>
    private void ApplyReasoning(ChatCompletionOptions options, LLMOptions opts)
    {
        var effort = (string.IsNullOrWhiteSpace(reasoningEffort) ? opts.ReasoningEffort : reasoningEffort)?.Trim().ToLowerInvariant();
        options.ReasoningEffortLevel = effort switch
        {
            "low" => ChatReasoningEffortLevel.Low,
            "medium" => ChatReasoningEffortLevel.Medium,
            "high" => ChatReasoningEffortLevel.High,
            _ => null,
        };
    }

    protected override async Task<LLMCompletionResult> CompleteAsyncCore(string systemPrompt, string userPrompt, LLMOptions opts, CancellationToken ct)
    {
        var targetModel = string.IsNullOrEmpty(opts.Model) ? model : opts.Model;
        var chatClient = CreateChatClient(targetModel);

        var messages = new List<ChatMessage>
        {
            ChatMessage.CreateSystemMessage(systemPrompt),
            ChatMessage.CreateUserMessage(userPrompt)
        };

        var completionOptions = new ChatCompletionOptions
        {
            MaxOutputTokenCount = opts.MaxTokens
        };
        ApplyReasoning(completionOptions, opts);
        if (opts.JsonMode)
        {
            completionOptions.ResponseFormat = opts.JsonSchema.HasValue
                ? ChatResponseFormat.CreateJsonSchemaFormat("response", BinaryData.FromString(opts.JsonSchema.Value.GetRawText()), jsonSchemaIsStrict: false)
                : ChatResponseFormat.CreateJsonObjectFormat();
        }

        var result = await chatClient.CompleteChatAsync(messages, completionOptions, ct);
        var completion = result.Value;

        if (completion.Usage is { } usage)
            UpdateTokenUsage(new TokenUsage(usage.InputTokenCount, usage.OutputTokenCount, usage.TotalTokenCount));

        // The Chat Completions API never returns reasoning/thinking text (o-series models
        // only expose that via the separate Responses API) — Reasoning is always null here.
        return new LLMCompletionResult(Text(completion), null);
    }

    private static string Text(ChatCompletion completion) =>
        string.Concat(completion.Content.Where(p => p.Kind == ChatMessageContentPartKind.Text).Select(p => p.Text));

    protected override async Task<LLMToolCallResult> CompleteWithToolsAsyncCore(string systemPrompt, string userPrompt, IEnumerable<ToolDefinition> tools, LLMOptions opts, CancellationToken ct)
    {
        var targetModel = string.IsNullOrEmpty(opts.Model) ? model : opts.Model;
        var chatClient = CreateChatClient(targetModel);

        var messages = new List<ChatMessage>
        {
            ChatMessage.CreateSystemMessage(systemPrompt),
            ChatMessage.CreateUserMessage(userPrompt)
        };

        var chatTools = tools.Select(t =>
            ChatTool.CreateFunctionTool(t.Name, t.Description, BinaryData.FromString(t.Parameters.GetRawText()))
        ).ToList();

        var completionOptions = new ChatCompletionOptions
        {
            MaxOutputTokenCount = opts.MaxTokens
        };
        ApplyReasoning(completionOptions, opts);

        foreach (var tool in chatTools)
            completionOptions.Tools.Add(tool);

        var result = await chatClient.CompleteChatAsync(messages, completionOptions, ct);
        var completion = result.Value;

        if (completion.Usage is { } usage)
            UpdateTokenUsage(new TokenUsage(usage.InputTokenCount, usage.OutputTokenCount, usage.TotalTokenCount));

        // Both, whatever the finish reason. Reading the calls only when it was "tool_calls" and the
        // text only otherwise lost the narration that comes with tool calls (the saga posts it
        // before the tools run) and, from gateways that report "stop", the calls themselves.
        var toolCalls = new List<ToolCall>();
        foreach (var tc in completion.ToolCalls)
        {
            JsonElement args;
            try
            {
                var raw = tc.FunctionArguments.ToString();
                args = JsonSerializer.Deserialize<JsonElement>(string.IsNullOrWhiteSpace(raw) ? "{}" : raw);
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "OpenAILLMProvider: unreadable arguments for {Tool}; calling it with none", tc.FunctionName);
                args = JsonSerializer.SerializeToElement(new { });
            }
            toolCalls.Add(new ToolCall(tc.Id, tc.FunctionName, args));
        }

        var text = Text(completion);
        return new LLMToolCallResult(text.Length > 0 ? text : null, toolCalls, GetTokenUsage());
    }

    public override async Task<float[]> GetEmbeddingAsync(string text, CancellationToken ct)
    {
        var targetModel = embeddingModel ?? model;
        var embeddingClient = CreateEmbeddingClient(targetModel);
        var result = await embeddingClient.GenerateEmbeddingAsync(text, cancellationToken: ct);
        return result.Value.ToFloats().ToArray();
    }

    public override async Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken ct)
    {
        try
        {
            var baseUrl = (endpointUrl ?? "https://api.openai.com/v1").TrimEnd('/');
            using var req = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/models");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
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
            logger.LogWarning(ex, "OpenAILLMProvider: failed to list models from {EndpointUrl}", endpointUrl ?? "https://api.openai.com/v1");
            return [];
        }
    }

    public override async Task<bool> IsAvailableAsync(CancellationToken ct)
    {
        try
        {
            await CompleteAsyncCore("ping", "ping", new LLMOptions { Model = model, MaxTokens = 1 }, ct);
            return true;
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
            await CompleteAsyncCore("ping", "ping", new LLMOptions { Model = model, MaxTokens = 1 }, ct);
            return new ProviderStatus(true, model, null);
        }
        catch (Exception ex)
        {
            return new ProviderStatus(false, null, ex.Message);
        }
    }
}
