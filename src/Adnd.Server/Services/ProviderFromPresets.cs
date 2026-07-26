namespace Adnd.Server.Services;

using Adnd.Server.Models;
using Adnd.Server.Services.Llm;

public class OllamaLLMProviderFromPreset : BaseLLMProvider
{
    private readonly IAdndLlmStrategy _strategy;
    private readonly LlmStrategyConfig _strategyConfig;
    private readonly string _model;
    private readonly string _baseUrl;

    public OllamaLLMProviderFromPreset(LLMPreset preset, ILogger<BaseLLMProvider>? logger = null, IConfiguration? configuration = null, IHttpClientFactory? httpClientFactory = null)
        : base(logger ?? new NullLogger<OllamaLLMProviderFromPreset>(), configuration ?? new NullConfiguration())
    {
        _baseUrl = preset.EndpointUrl ?? "http://localhost:11434";
        _model = preset.BaseModel;
        _strategy = AdndLlmStrategyFactory.Get("ollama");
        _strategyConfig = new LlmStrategyConfig(
            Endpoint: _baseUrl,
            ModelName: _model,
            ApiKey: null,
            EmbeddingModel: preset.EmbeddingModel,
            EmbeddingEndpoint: preset.EmbeddingEndpointUrl,
            TimeoutMs: preset.TimeoutMs,
            ReasoningEffort: preset.ReasoningEffort
        );
    }

    public override string ProviderId => "ollama";
    public override string EndpointUrl => _baseUrl;
    public override string ModelName => _model;
    public override (int, int, int)? GetTokenUsage(string responseText) => null;

    protected override Task<string> CompleteAsyncCore(string systemPrompt, string userPrompt, LLMOptions? options = null) =>
        _strategy.CompleteAsync(BuildRequest(systemPrompt, userPrompt, options));

    protected override async Task<LLMCompletionResult> CompleteWithToolsAsyncCore(
        string systemPrompt, string userPrompt, IEnumerable<GMToolDefinition> tools, LLMOptions? options = null)
    {
        var result = await _strategy.CompleteWithToolsAsync(BuildRequest(systemPrompt, userPrompt, options), tools);
        return new LLMCompletionResult { Content = result.Content, ToolCalls = result.ToolCalls, TokenUsage = result.TokenUsage };
    }

    protected override Task<float[]> GetEmbeddingAsyncCore(string text) =>
        _strategy.GetEmbeddingAsync(text, _strategyConfig);

    public override Task<bool> IsAvailableAsync() => Task.FromResult(true);
    public override Task<ProviderStatus> GetStatusAsync() =>
        Task.FromResult(new ProviderStatus { ProviderId = ProviderId, Model = _model, IsAvailable = true, CheckedAt = DateTime.UtcNow });

    private LlmStrategyRequest BuildRequest(string systemPrompt, string userPrompt, LLMOptions? options) => new(
        Config: _strategyConfig,
        SystemPrompt: systemPrompt,
        UserPrompt: userPrompt,
        Temperature: options?.Temperature > 0 ? options.Temperature : 0.7f,
        MaxTokens: options?.MaxTokens > 0 ? options.MaxTokens : 2048,
        JsonSchema: options?.JsonSchemaOutput
    );
}

public class LmStudioLLMProviderFromPreset : BaseLLMProvider
{
    private readonly IAdndLlmStrategy _strategy;
    private readonly LlmStrategyConfig _strategyConfig;
    private readonly string _model;
    private readonly string _baseUrl;

    public LmStudioLLMProviderFromPreset(LLMPreset preset, ILogger<BaseLLMProvider>? logger = null, IConfiguration? configuration = null, IHttpClientFactory? httpClientFactory = null)
        : base(logger ?? new NullLogger<LmStudioLLMProviderFromPreset>(), configuration ?? new NullConfiguration())
    {
        _baseUrl = preset.EndpointUrl ?? "http://localhost:1234";
        _model = preset.BaseModel;
        _strategy = AdndLlmStrategyFactory.Get("lmstudio");
        _strategyConfig = new LlmStrategyConfig(
            Endpoint: _baseUrl,
            ModelName: _model,
            ApiKey: preset.DecryptedApiKey ?? preset.ApiKey,
            EmbeddingModel: preset.EmbeddingModel,
            EmbeddingEndpoint: preset.EmbeddingEndpointUrl,
            TimeoutMs: preset.TimeoutMs,
            ReasoningEffort: preset.ReasoningEffort
        );
    }

    public override string ProviderId => "lmstudio";
    public override string EndpointUrl => _baseUrl;
    public override string ModelName => _model;
    public override (int, int, int)? GetTokenUsage(string responseText) => null;

    protected override Task<string> CompleteAsyncCore(string systemPrompt, string userPrompt, LLMOptions? options = null) =>
        _strategy.CompleteAsync(BuildRequest(systemPrompt, userPrompt, options));

    protected override async Task<LLMCompletionResult> CompleteWithToolsAsyncCore(
        string systemPrompt, string userPrompt, IEnumerable<GMToolDefinition> tools, LLMOptions? options = null)
    {
        var result = await _strategy.CompleteWithToolsAsync(BuildRequest(systemPrompt, userPrompt, options), tools);
        return new LLMCompletionResult { Content = result.Content, ToolCalls = result.ToolCalls, TokenUsage = result.TokenUsage };
    }

    protected override Task<float[]> GetEmbeddingAsyncCore(string text) =>
        _strategy.GetEmbeddingAsync(text, _strategyConfig);

    public override Task<bool> IsAvailableAsync() => Task.FromResult(true);
    public override Task<ProviderStatus> GetStatusAsync() =>
        Task.FromResult(new ProviderStatus { ProviderId = ProviderId, Model = _model, IsAvailable = true, CheckedAt = DateTime.UtcNow });

    private LlmStrategyRequest BuildRequest(string systemPrompt, string userPrompt, LLMOptions? options) => new(
        Config: _strategyConfig,
        SystemPrompt: systemPrompt,
        UserPrompt: userPrompt,
        Temperature: options?.Temperature > 0 ? options.Temperature : 0.7f,
        MaxTokens: options?.MaxTokens > 0 ? options.MaxTokens : 2048,
        JsonSchema: options?.JsonSchemaOutput
    );
}

public class OpenAILLMProviderFromPreset : BaseLLMProvider
{
    private readonly IAdndLlmStrategy _strategy;
    private readonly LlmStrategyConfig _strategyConfig;
    private readonly string _model;
    private readonly string _baseUrl;

    public OpenAILLMProviderFromPreset(LLMPreset preset, ILogger<BaseLLMProvider>? logger = null, IConfiguration? configuration = null, IHttpClientFactory? httpClientFactory = null)
        : base(logger ?? new NullLogger<OpenAILLMProviderFromPreset>(), configuration ?? new NullConfiguration())
    {
        _baseUrl = preset.EndpointUrl ?? "https://api.openai.com/v1";
        _model = preset.BaseModel;
        _strategy = AdndLlmStrategyFactory.Get("openai");
        _strategyConfig = new LlmStrategyConfig(
            Endpoint: _baseUrl,
            ModelName: _model,
            ApiKey: preset.DecryptedApiKey ?? preset.ApiKey,
            EmbeddingModel: preset.EmbeddingModel,
            EmbeddingEndpoint: preset.EmbeddingEndpointUrl,
            TimeoutMs: preset.TimeoutMs,
            ReasoningEffort: preset.ReasoningEffort
        );
    }

    public override string ProviderId => "openai";
    public override string EndpointUrl => _baseUrl;
    public override string ModelName => _model;
    public override (int, int, int)? GetTokenUsage(string responseText) => null;

    protected override Task<string> CompleteAsyncCore(string systemPrompt, string userPrompt, LLMOptions? options = null) =>
        _strategy.CompleteAsync(BuildRequest(systemPrompt, userPrompt, options));

    protected override async Task<LLMCompletionResult> CompleteWithToolsAsyncCore(
        string systemPrompt, string userPrompt, IEnumerable<GMToolDefinition> tools, LLMOptions? options = null)
    {
        var result = await _strategy.CompleteWithToolsAsync(BuildRequest(systemPrompt, userPrompt, options), tools);
        return new LLMCompletionResult { Content = result.Content, ToolCalls = result.ToolCalls, TokenUsage = result.TokenUsage };
    }

    protected override Task<float[]> GetEmbeddingAsyncCore(string text) =>
        _strategy.GetEmbeddingAsync(text, _strategyConfig);

    public override Task<bool> IsAvailableAsync() => Task.FromResult(true);
    public override Task<ProviderStatus> GetStatusAsync() =>
        Task.FromResult(new ProviderStatus { ProviderId = ProviderId, Model = _model, IsAvailable = true, CheckedAt = DateTime.UtcNow });

    private LlmStrategyRequest BuildRequest(string systemPrompt, string userPrompt, LLMOptions? options) => new(
        Config: _strategyConfig,
        SystemPrompt: systemPrompt,
        UserPrompt: userPrompt,
        Temperature: options?.Temperature > 0 ? options.Temperature : 0.7f,
        MaxTokens: options?.MaxTokens > 0 ? options.MaxTokens : 2048,
        JsonSchema: options?.JsonSchemaOutput
    );
}

public class GoogleAIStudioLLMProviderFromPreset : BaseLLMProvider
{
    private readonly IAdndLlmStrategy _strategy;
    private readonly LlmStrategyConfig _strategyConfig;
    private readonly string _model;
    private readonly string _baseUrl;

    public GoogleAIStudioLLMProviderFromPreset(LLMPreset preset, ILogger<BaseLLMProvider>? logger = null, IConfiguration? configuration = null, IHttpClientFactory? httpClientFactory = null)
        : base(logger ?? new NullLogger<GoogleAIStudioLLMProviderFromPreset>(), configuration ?? new NullConfiguration())
    {
        _baseUrl = preset.EndpointUrl ?? "https://generativelanguage.googleapis.com/v1beta/openai";
        _model = preset.BaseModel;
        _strategy = AdndLlmStrategyFactory.Get("google");
        _strategyConfig = new LlmStrategyConfig(
            Endpoint: _baseUrl,
            ModelName: _model,
            ApiKey: preset.DecryptedApiKey ?? preset.ApiKey,
            EmbeddingModel: preset.EmbeddingModel,
            EmbeddingEndpoint: preset.EmbeddingEndpointUrl,
            TimeoutMs: preset.TimeoutMs,
            ReasoningEffort: preset.ReasoningEffort
        );
    }

    public override string ProviderId => "google";
    public override string EndpointUrl => _baseUrl;
    public override string ModelName => _model;
    public override (int, int, int)? GetTokenUsage(string responseText) => null;

    protected override Task<string> CompleteAsyncCore(string systemPrompt, string userPrompt, LLMOptions? options = null) =>
        _strategy.CompleteAsync(BuildRequest(systemPrompt, userPrompt, options));

    protected override async Task<LLMCompletionResult> CompleteWithToolsAsyncCore(
        string systemPrompt, string userPrompt, IEnumerable<GMToolDefinition> tools, LLMOptions? options = null)
    {
        var result = await _strategy.CompleteWithToolsAsync(BuildRequest(systemPrompt, userPrompt, options), tools);
        return new LLMCompletionResult { Content = result.Content, ToolCalls = result.ToolCalls, TokenUsage = result.TokenUsage };
    }

    protected override Task<float[]> GetEmbeddingAsyncCore(string text) =>
        _strategy.GetEmbeddingAsync(text, _strategyConfig);

    public override Task<bool> IsAvailableAsync() => Task.FromResult(true);
    public override Task<ProviderStatus> GetStatusAsync() =>
        Task.FromResult(new ProviderStatus { ProviderId = ProviderId, Model = _model, IsAvailable = true, CheckedAt = DateTime.UtcNow });

    private LlmStrategyRequest BuildRequest(string systemPrompt, string userPrompt, LLMOptions? options) => new(
        Config: _strategyConfig,
        SystemPrompt: systemPrompt,
        UserPrompt: userPrompt,
        Temperature: options?.Temperature > 0 ? options.Temperature : 0.7f,
        MaxTokens: options?.MaxTokens > 0 ? options.MaxTokens : 2048,
        JsonSchema: options?.JsonSchemaOutput
    );
}

// ==================== API Response Types ====================
