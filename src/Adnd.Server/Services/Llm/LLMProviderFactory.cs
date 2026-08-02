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
                httpClientFactory.CreateClient("LLMProvider"),
                loggerFactory.CreateLogger<OllamaLLMProvider>()),
            "openaicompatible" => new OpenAICompatibleLLMProvider(
                NullOrEmpty(preset.EndpointUrl) ?? "http://localhost:11434/v1",
                preset.DecryptedApiKey,
                NullOrEmpty(preset.EmbeddingEndpointUrl),
                NullOrEmpty(preset.EmbeddingModel),
                httpClientFactory.CreateClient("LLMProvider"),
                loggerFactory.CreateLogger<OpenAICompatibleLLMProvider>()),
            "openai" => new OpenAILLMProvider(
                preset.DecryptedApiKey ?? string.Empty,
                preset.BaseModel,
                NullOrEmpty(preset.EndpointUrl),
                NullOrEmpty(preset.EmbeddingModel),
                httpClientFactory.CreateClient("LLMProvider"),
                loggerFactory.CreateLogger<OpenAILLMProvider>()),
            "google" => new GoogleAIStudioLLMProvider(
                preset.DecryptedApiKey ?? string.Empty,
                preset.BaseModel,
                NullOrEmpty(preset.EmbeddingModel),
                httpClientFactory.CreateClient("LLMProvider"),
                loggerFactory.CreateLogger<GoogleAIStudioLLMProvider>()),
            _ => throw new InvalidOperationException($"Unknown LLM provider type: {preset.ProviderType}")
        };

    private static string? NullOrEmpty(string? s) => string.IsNullOrEmpty(s) ? null : s;
}
