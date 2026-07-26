#pragma warning disable OPENAI001
using System.Text.Json;
using Adnd.Server.Models;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;

namespace Adnd.Server.Services.Llm;

/// <summary>
/// OpenAI strategy using the official OpenAI SDK.
/// Supports native tool calling, ReasoningEffort, and JSON schema output.
/// </summary>
public class OpenAIStrategy : IAdndLlmStrategy
{
    public string ProviderKey => "openai";

    public async Task<string> CompleteAsync(LlmStrategyRequest request, CancellationToken ct = default)
    {
        var chatClient = BuildChatClient(request.Config);
        var messages = BuildMessages(request.SystemPrompt, request.UserPrompt);
        var options = BuildOptions(request);

        var result = await chatClient.CompleteChatAsync(messages, options, ct);
        return result.Value.Content.FirstOrDefault()?.Text ?? "";
    }

    public async Task<LlmStrategyToolResult> CompleteWithToolsAsync(
        LlmStrategyRequest request, IEnumerable<GMToolDefinition> tools, CancellationToken ct = default)
    {
        var toolList = tools.ToList();
        var chatClient = BuildChatClient(request.Config);
        var messages = BuildMessages(request.SystemPrompt, request.UserPrompt);
        var options = BuildOptions(request);

        foreach (var t in toolList)
        {
            options.Tools.Add(ChatTool.CreateFunctionTool(
                t.Name,
                t.Description,
                BinaryData.FromString(JsonSerializer.Serialize(t.Parameters))));
        }

        if (toolList.Count > 0)
            options.ToolChoice = ChatToolChoice.CreateAutoChoice();

        var result = await chatClient.CompleteChatAsync(messages, options, ct);
        var choice = result.Value;

        var content = choice.Content.FirstOrDefault()?.Text ?? "";
        var toolCalls = choice.ToolCalls.Select(tc => new ToolCall
        {
            Id = tc.Id ?? $"call_{Guid.NewGuid():N}"[..8],
            Name = tc.FunctionName ?? "unknown",
            Arguments = tc.FunctionArguments.ToString(),
        }).ToList();

        (int, int, int)? usage = null;
        if (choice.Usage is { } u)
            usage = (u.InputTokenCount, u.OutputTokenCount, u.TotalTokenCount);

        return new LlmStrategyToolResult(content, toolCalls, usage);
    }

    public async Task<float[]> GetEmbeddingAsync(string text, LlmStrategyConfig config, CancellationToken ct = default)
    {
        var embeddingEndpoint = config.EmbeddingEndpoint ?? config.Endpoint;
        var embeddingModel = config.EmbeddingModel ?? "text-embedding-3-small";
        var openAiClient = BuildOpenAIClient(embeddingEndpoint, config.ApiKey);
        var embeddingClient = openAiClient.GetEmbeddingClient(embeddingModel);

        var result = await embeddingClient.GenerateEmbeddingAsync(text, cancellationToken: ct);
        return result.Value.ToFloats().ToArray();
    }

    private OpenAIClient BuildOpenAIClient(string? endpoint, string? apiKey)
    {
        var credential = new ApiKeyCredential(apiKey ?? "none");
        if (string.IsNullOrWhiteSpace(endpoint))
            return new OpenAIClient(credential);

        var clientOptions = new OpenAIClientOptions { Endpoint = new Uri(endpoint.TrimEnd('/')) };
        return new OpenAIClient(credential, clientOptions);
    }

    private ChatClient BuildChatClient(LlmStrategyConfig config)
        => BuildOpenAIClient(config.Endpoint, config.ApiKey).GetChatClient(config.ModelName);

    private static List<ChatMessage> BuildMessages(string systemPrompt, string userPrompt) =>
    [
        ChatMessage.CreateSystemMessage(systemPrompt),
        ChatMessage.CreateUserMessage(userPrompt),
    ];

    private static ChatCompletionOptions BuildOptions(LlmStrategyRequest request)
    {
        var options = new ChatCompletionOptions
        {
            Temperature = request.Temperature,
            MaxOutputTokenCount = request.MaxTokens,
        };

        if (request.JsonSchema != null)
            options.ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                request.JsonSchema.Name,
                BinaryData.FromString(request.JsonSchema.GetSchemaString()));

        if (!string.IsNullOrWhiteSpace(request.Config.ReasoningEffort) && request.Config.ReasoningEffort != "none")
            options.ReasoningEffortLevel = request.Config.ReasoningEffort switch
            {
                "low" => ChatReasoningEffortLevel.Low,
                "medium" => ChatReasoningEffortLevel.Medium,
                _ => ChatReasoningEffortLevel.High,
            };

        return options;
    }
}
