using System.Text;
using OllamaSharp;
using OllamaSharp.Models;
using OllamaSharp.Models.Chat;

namespace Adnd.Server.Services.Llm;

public class OllamaLLMProvider(string endpointUrl, string model, string? embeddingModel) : BaseLLMProvider
{
    private readonly string _embeddingModel = embeddingModel ?? model;

    public override string ProviderId => "ollama";
    public override string EndpointUrl => endpointUrl;

    protected override async Task<string> CompleteAsyncCore(string systemPrompt, string userPrompt, LLMOptions opts, CancellationToken ct)
    {
        var client = new OllamaApiClient(endpointUrl);
        var messages = new List<Message>
        {
            new() { Role = ChatRole.System, Content = systemPrompt },
            new() { Role = ChatRole.User, Content = userPrompt }
        };

        var request = new ChatRequest
        {
            Model = string.IsNullOrEmpty(opts.Model) ? model : opts.Model,
            Messages = messages,
            Stream = false
        };

        var sb = new StringBuilder();
        ChatDoneResponseStream? done = null;

        await foreach (var chunk in client.ChatAsync(request, ct))
        {
            if (chunk is null)
                continue;
            if (chunk.Message?.Content is { } content)
                sb.Append(content);
            if (chunk is ChatDoneResponseStream doneChunk)
                done = doneChunk;
        }

        if (done is not null)
        {
            var promptTokens = done.PromptEvalCount;
            var completionTokens = done.EvalCount;
            UpdateTokenUsage(new TokenUsage(promptTokens, completionTokens, promptTokens + completionTokens));
        }

        return sb.ToString();
    }

    protected override Task<LLMToolCallResult> CompleteWithToolsAsyncCore(string systemPrompt, string userPrompt, IEnumerable<ToolDefinition> tools, LLMOptions opts, CancellationToken ct)
        => throw new NotSupportedException("Ollama does not natively support tool calling via this provider.");

    public override async Task<float[]> GetEmbeddingAsync(string text, CancellationToken ct)
    {
        var client = new OllamaApiClient(endpointUrl);
        var request = new EmbedRequest { Model = _embeddingModel, Input = [text] };
        var response = await client.EmbedAsync(request, ct);
        return response?.Embeddings?.FirstOrDefault() ?? [];
    }

    public override async Task<bool> IsAvailableAsync(CancellationToken ct)
    {
        try
        {
            var client = new OllamaApiClient(endpointUrl);
            await client.ListLocalModelsAsync(ct);
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
            var client = new OllamaApiClient(endpointUrl);
            await client.ListLocalModelsAsync(ct);
            return new ProviderStatus(true, model, null);
        }
        catch (Exception ex)
        {
            return new ProviderStatus(false, null, ex.Message);
        }
    }
}
