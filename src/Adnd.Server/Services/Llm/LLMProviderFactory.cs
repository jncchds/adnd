using Adnd.Server.Models;

namespace Adnd.Server.Services.Llm;

public interface ILLMProviderFactory
{
    ILLMProvider CreateFromPreset(LLMPreset preset);
}

public class LLMProviderFactory(IHttpClientFactory httpClientFactory) : ILLMProviderFactory
{
    public ILLMProvider CreateFromPreset(LLMPreset preset) =>
        preset.ProviderType.ToLowerInvariant() switch
        {
            "ollama" => new OllamaLLMProvider(
                preset.EndpointUrl ?? "http://localhost:11434",
                preset.BaseModel,
                preset.EmbeddingModel),
            "openaicompatible" => new OpenAICompatibleLLMProvider(
                preset.EndpointUrl ?? "http://localhost:11434/v1",
                preset.DecryptedApiKey,
                preset.EmbeddingEndpointUrl,
                httpClientFactory.CreateClient("LLMProvider")),
            "openai" => new OpenAILLMProvider(
                preset.DecryptedApiKey ?? string.Empty,
                preset.BaseModel,
                preset.EndpointUrl,
                preset.EmbeddingModel),
            "google" => new GoogleAIStudioLLMProvider(
                preset.DecryptedApiKey ?? string.Empty,
                preset.BaseModel,
                httpClientFactory.CreateClient("LLMProvider")),
            _ => throw new InvalidOperationException($"Unknown LLM provider type: {preset.ProviderType}")
        };
}
