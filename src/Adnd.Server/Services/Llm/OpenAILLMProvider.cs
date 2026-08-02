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
    HttpClient httpClient,
    ILogger<OpenAILLMProvider> logger) : BaseLLMProvider
{
    public override string ProviderId => "openai";
    public override string EndpointUrl => endpointUrl ?? "https://api.openai.com/v1";

    private ChatClient CreateChatClient(string targetModel)
    {
        if (!string.IsNullOrEmpty(endpointUrl))
        {
            var options = new OpenAIClientOptions { Endpoint = new Uri(endpointUrl) };
            return new ChatClient(targetModel, new System.ClientModel.ApiKeyCredential(apiKey), options);
        }
        return new ChatClient(targetModel, apiKey);
    }

    private EmbeddingClient CreateEmbeddingClient(string targetModel)
    {
        if (!string.IsNullOrEmpty(endpointUrl))
        {
            var options = new OpenAIClientOptions { Endpoint = new Uri(endpointUrl) };
            return new EmbeddingClient(targetModel, new System.ClientModel.ApiKeyCredential(apiKey), options);
        }
        return new EmbeddingClient(targetModel, apiKey);
    }

    protected override async Task<string> CompleteAsyncCore(string systemPrompt, string userPrompt, LLMOptions opts, CancellationToken ct)
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

        return completion.Content.Count > 0 ? completion.Content[0].Text : string.Empty;
    }

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

        foreach (var tool in chatTools)
            completionOptions.Tools.Add(tool);

        var result = await chatClient.CompleteChatAsync(messages, completionOptions, ct);
        var completion = result.Value;

        if (completion.Usage is { } usage)
            UpdateTokenUsage(new TokenUsage(usage.InputTokenCount, usage.OutputTokenCount, usage.TotalTokenCount));

        string? narrativeText = null;
        var toolCalls = new List<ToolCall>();

        if (completion.FinishReason == ChatFinishReason.ToolCalls)
        {
            foreach (var tc in completion.ToolCalls)
            {
                var args = JsonSerializer.Deserialize<JsonElement>(tc.FunctionArguments.ToString());
                toolCalls.Add(new ToolCall(tc.Id, tc.FunctionName, args));
            }
        }
        else
        {
            narrativeText = completion.Content.Count > 0 ? completion.Content[0].Text : string.Empty;
        }

        return new LLMToolCallResult(narrativeText, toolCalls, GetTokenUsage());
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
            var baseUrl = endpointUrl ?? "https://api.openai.com/v1";
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
