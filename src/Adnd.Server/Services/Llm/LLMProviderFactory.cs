using Adnd.Server.Models;
using Microsoft.Extensions.Logging;

namespace Adnd.Server.Services.Llm;

public interface ILLMProviderFactory
{
    ILLMProvider CreateFromPreset(LLMPreset preset);
}

public class LLMProviderFactory(IHttpClientFactory httpClientFactory, ILoggerFactory loggerFactory) : ILLMProviderFactory
{
    public ILLMProvider CreateFromPreset(LLMPreset preset) =>
        preset.ProviderType.ToLowerInvariant() switch
        {
            "ollama" => new OllamaLLMProvider(
                NullOrEmpty(preset.EndpointUrl) ?? "http://localhost:11434",
                preset.BaseModel,
                preset.EmbeddingModel,
                Client(preset),
                loggerFactory.CreateLogger<OllamaLLMProvider>()),
            "openaicompatible" => new OpenAICompatibleLLMProvider(
                NullOrEmpty(preset.EndpointUrl) ?? "http://localhost:11434/v1",
                preset.DecryptedApiKey,
                NullOrEmpty(preset.EmbeddingEndpointUrl),
                NullOrEmpty(preset.EmbeddingModel),
                NullOrEmpty(preset.ReasoningEffort),
                Client(preset),
                loggerFactory.CreateLogger<OpenAICompatibleLLMProvider>()),
            "openai" => new OpenAILLMProvider(
                preset.DecryptedApiKey ?? string.Empty,
                preset.BaseModel,
                NullOrEmpty(preset.EndpointUrl),
                NullOrEmpty(preset.EmbeddingModel),
                Client(preset),
                loggerFactory.CreateLogger<OpenAILLMProvider>()),
            "google" => new GoogleAIStudioLLMProvider(
                preset.DecryptedApiKey ?? string.Empty,
                preset.BaseModel,
                NullOrEmpty(preset.EmbeddingModel),
                Client(preset),
                loggerFactory.CreateLogger<GoogleAIStudioLLMProvider>()),
            _ => throw new InvalidOperationException($"Unknown LLM provider type: {preset.ProviderType}")
        };

    /// <summary>The preset's own timeout, when it has one. It was stored and never applied, so
    /// every provider ran on the client's default however slow the model behind it.</summary>
    private HttpClient Client(LLMPreset preset)
    {
        var client = httpClientFactory.CreateClient("LLMProvider");
        if (preset.TimeoutMs is > 0)
            client.Timeout = TimeSpan.FromMilliseconds(preset.TimeoutMs.Value);
        return client;
    }

    private static string? NullOrEmpty(string? s) => string.IsNullOrEmpty(s) ? null : s;
}
