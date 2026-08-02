using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OllamaSharp;
using OllamaSharp.Models;
using OllamaSharp.Models.Chat;

namespace Adnd.Server.Services.Llm;

public class OllamaLLMProvider : BaseLLMProvider
{
    private readonly string _endpointUrl;
    private readonly string _model;
    private readonly string _embeddingModel;
    private readonly ILogger<OllamaLLMProvider> _logger;

    /// <summary>
    /// Built once over the pooled HttpClient. OllamaApiClient(string) creates and owns its
    /// own HttpClient and is IDisposable; constructing one per call (and never disposing
    /// it) leaked a socket per request — an embedding backfill of 50 messages left 50
    /// connections in TIME_WAIT. Ollama was the only provider not given the pooled client.
    /// </summary>
    private readonly OllamaApiClient _client;

    public OllamaLLMProvider(
        string endpointUrl,
        string model,
        string? embeddingModel,
        HttpClient httpClient,
        ILogger<OllamaLLMProvider> logger)
    {
        _endpointUrl = endpointUrl;
        _model = model;
        _embeddingModel = embeddingModel ?? model;
        _logger = logger;

        httpClient.BaseAddress = new Uri(endpointUrl);
        _client = new OllamaApiClient(httpClient);
    }

    public override string ProviderId => "ollama";
    public override string EndpointUrl => _endpointUrl;

    protected override async Task<string> CompleteAsyncCore(string systemPrompt, string userPrompt, LLMOptions opts, CancellationToken ct)
    {
        var messages = new List<Message>
        {
            new() { Role = ChatRole.System, Content = systemPrompt },
            new() { Role = ChatRole.User, Content = userPrompt }
        };

        var request = new ChatRequest
        {
            Model = string.IsNullOrEmpty(opts.Model) ? _model : opts.Model,
            Messages = messages,
            Stream = false,
            Format = opts.JsonMode ? JsonSerializer.Deserialize<JsonElement>("\"json\"") : null
        };

        var sb = new StringBuilder();
        ChatDoneResponseStream? done = null;

        await foreach (var chunk in _client.ChatAsync(request, ct))
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
        var request = new EmbedRequest { Model = _embeddingModel, Input = [text] };
        var response = await _client.EmbedAsync(request, ct);
        return response?.Embeddings?.FirstOrDefault() ?? [];
    }

    public override async Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken ct)
    {
        try
        {
            var models = await _client.ListLocalModelsAsync(ct);
            return models.Select(m => m.Name).OfType<string>().OrderBy(n => n).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OllamaLLMProvider: failed to list models from {EndpointUrl}", _endpointUrl);
            return [];
        }
    }

    public override async Task<bool> IsAvailableAsync(CancellationToken ct)
    {
        try
        {
            await _client.ListLocalModelsAsync(ct);
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
            await _client.ListLocalModelsAsync(ct);
            return new ProviderStatus(true, _model, null);
        }
        catch (Exception ex)
        {
            return new ProviderStatus(false, null, ex.Message);
        }
    }
}
