using Microsoft.Extensions.Logging;
using Adnd.Server.Models;
using System.Security.Cryptography;

namespace Adnd.Server.Services;

/// <summary>
/// Factory for creating ILLMProvider instances from LLMPreset configurations.
/// Used by GameAgent, AgentBus, and other runtime consumers to instantiate
/// the correct provider (Ollama, LM Studio, OpenAI, Google) with the
/// preset's decrypted API key and model settings.
/// </summary>
public interface ILLMProviderFactory
{
    /// <summary>
    /// Create an ILLMProvider from a loaded LLMPreset entity.
    /// The preset's API key must already be decrypted (DecryptedApiKey set).
    /// </summary>
    ILLMProvider CreateFromPreset(LLMPreset preset);
}

public class LLMProviderFactory : ILLMProviderFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;

    public LLMProviderFactory(ILoggerFactory loggerFactory, IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        _loggerFactory = loggerFactory;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
    }

    public ILLMProvider CreateFromPreset(LLMPreset preset)
    {
        var logger = _loggerFactory.CreateLogger<BaseLLMProvider>();

        return preset.ProviderType.ToLowerInvariant() switch
        {
            "ollama" => new OllamaLLMProviderFromPreset(preset, logger, _configuration, _httpClientFactory),
            "lmstudio" => new LmStudioLLMProviderFromPreset(preset, logger, _configuration, _httpClientFactory),
            "openai" => new OpenAILLMProviderFromPreset(preset, logger, _configuration, _httpClientFactory),
            "google" => new GoogleAIStudioLLMProviderFromPreset(preset, logger, _configuration, _httpClientFactory),
            _ => throw new ArgumentException($"Unknown provider type: {preset.ProviderType}")
        };
    }
}
