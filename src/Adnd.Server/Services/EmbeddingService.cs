using Adnd.Server.Data;
using Adnd.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Services;

/// <summary>
/// Service for generating embeddings using the LLM preset's embedding model.
/// Each game's LLM preset specifies its own embedding model and endpoint,
/// so embeddings are always generated with the correct model for that game.
/// </summary>
public interface IEmbeddingService
{
    /// <summary>
    /// Generate an embedding for a single text using the game's preset embedding model.
    /// Returns the raw vector from the model (dimension varies by model).
    /// </summary>
    Task<float[]> GenerateEmbeddingAsync(Guid gameId, string text);

    /// <summary>
    /// Generate embeddings for multiple texts using the game's preset embedding model.
    /// Returns vectors in the same order as input texts.
    /// </summary>
    Task<List<float[]>> GenerateEmbeddingsAsync(Guid gameId, IEnumerable<string> texts);

    /// <summary>
    /// Generate an embedding using a specific preset (for cross-game operations).
    /// </summary>
    Task<float[]> GenerateEmbeddingWithPresetAsync(LLMPreset preset, string text);
}

public class EmbeddingService : IEmbeddingService
{
    private readonly AppDbContext _context;
    private readonly ILLMProviderRegistry _llmRegistry;
    private readonly ILogger<EmbeddingService> _logger;

    public EmbeddingService(
        AppDbContext context,
        ILLMProviderRegistry llmRegistry,
        ILogger<EmbeddingService> logger)
    {
        _context = context;
        _llmRegistry = llmRegistry;
        _logger = logger;
    }

    public async Task<float[]> GenerateEmbeddingAsync(Guid gameId, string text)
    {
        var game = await _context.Games
            .Include(g => g.LLMPreset)
            .FirstOrDefaultAsync(g => g.Id == gameId);

        if (game == null)
            throw new KeyNotFoundException($"Game {gameId} not found.");

        if (game.LLMPreset == null)
            throw new InvalidOperationException($"Game {gameId} has no LLM preset configured.");

        if (string.IsNullOrEmpty(game.LLMPreset.EmbeddingModel))
            throw new InvalidOperationException(
                $"Game {gameId}'s preset '{game.LLMPreset.Name}' has no embedding model configured. " +
                "Configure an EmbeddingModel in the LLM preset settings.");

        return await GenerateEmbeddingWithPresetAsync(game.LLMPreset, text);
    }

    public async Task<List<float[]>> GenerateEmbeddingsAsync(Guid gameId, IEnumerable<string> texts)
    {
        var textList = texts.ToList();
        if (!textList.Any())
            return new List<float[]>();

        var game = await _context.Games
            .Include(g => g.LLMPreset)
            .FirstOrDefaultAsync(g => g.Id == gameId);

        if (game == null)
            throw new KeyNotFoundException($"Game {gameId} not found.");

        if (game.LLMPreset == null)
            throw new InvalidOperationException($"Game {gameId} has no LLM preset configured.");

        if (string.IsNullOrEmpty(game.LLMPreset.EmbeddingModel))
            throw new InvalidOperationException(
                $"Game {gameId}'s preset '{game.LLMPreset.Name}' has no embedding model configured.");

        var results = new List<float[]>();
        foreach (var text in textList)
        {
            var embedding = await GenerateEmbeddingWithPresetAsync(game.LLMPreset, text);
            results.Add(embedding);
        }
        return results;
    }

    public async Task<float[]> GenerateEmbeddingWithPresetAsync(LLMPreset preset, string text)
    {
        // First try the registry (configured providers)
        var provider = _llmRegistry.GetProvider(preset.ProviderType);
        if (provider != null)
        {
            try
            {
                var embedding = await provider.GetEmbeddingAsync(text);
                if (embedding != null && embedding.Length > 0)
                    return embedding;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Registry provider '{Provider}' failed for embedding, trying preset wrapper", preset.ProviderType);
            }
        }

        // Fall back to preset-based wrapper
        return preset.ProviderType switch
        {
            "ollama" => await GenerateWithOllamaPreset(preset, text),
            "lmstudio" => await GenerateWithLmStudioPreset(preset, text),
            "openai" => await GenerateWithOpenAIPreset(preset, text),
            "google" => await GenerateWithGooglePreset(preset, text),
            _ => throw new InvalidOperationException($"Unknown provider type '{preset.ProviderType}' for embedding generation.")
        };
    }

    private async Task<float[]> GenerateWithOllamaPreset(LLMPreset preset, string text)
    {
        var baseUrl = preset.EndpointUrl ?? "http://localhost:11434";
        var embeddingUrl = preset.EmbeddingEndpointUrl ?? baseUrl;
        var model = preset.EmbeddingModel ?? "nomic-embed-text";

        var payload = new { model, input = text };
        var httpClient = new HttpClient();
        var response = await httpClient.PostAsJsonAsync(
            embeddingUrl + "/api/embed", payload);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError("Ollama embedding failed: {Status} - {Error}", response.StatusCode, error);
            throw new InvalidOperationException($"Ollama embedding failed: {error}");
        }

        var result = await response.Content.ReadFromJsonAsync<OllamaEmbeddingResponse>();
        var embedding = result?.Embedding;
        if (embedding == null || embedding.Length == 0)
        {
            throw new InvalidOperationException("Ollama returned empty embedding.");
        }
        return embedding;
    }

    private async Task<float[]> GenerateWithLmStudioPreset(LLMPreset preset, string text)
    {
        var baseUrl = preset.EndpointUrl ?? "http://localhost:1234";
        var embeddingUrl = preset.EmbeddingEndpointUrl ?? baseUrl;
        var model = preset.EmbeddingModel ?? "nomic-embed-text";

        var payload = new { model, input = text };
        var httpClient = new HttpClient();
        if (!string.IsNullOrEmpty(preset.ApiKey))
            httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", preset.ApiKey);

        var response = await httpClient.PostAsJsonAsync(
            embeddingUrl + "/v1/embeddings", payload);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError("LM Studio embedding failed: {Status} - {Error}", response.StatusCode, error);
            throw new InvalidOperationException($"LM Studio embedding failed: {error}");
        }

        var result = await response.Content.ReadFromJsonAsync<OpenAIEmbeddingResponse>();
        var embedding = result?.Data?.FirstOrDefault()?.Embedding;
        if (embedding == null || embedding.Length == 0)
        {
            throw new InvalidOperationException("LM Studio returned empty embedding.");
        }
        return embedding;
    }

    private async Task<float[]> GenerateWithOpenAIPreset(LLMPreset preset, string text)
    {
        var baseUrl = preset.EndpointUrl ?? "https://api.openai.com/v1";
        var embeddingUrl = preset.EmbeddingEndpointUrl ?? baseUrl;
        var model = preset.EmbeddingModel ?? "text-embedding-3-small";

        var payload = new { model, input = new[] { text } };
        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", preset.ApiKey ?? throw new InvalidOperationException("API key required for OpenAI"));

        var response = await httpClient.PostAsJsonAsync(
            embeddingUrl + "/v1/embeddings", payload);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError("OpenAI embedding failed: {Status} - {Error}", response.StatusCode, error);
            throw new InvalidOperationException($"OpenAI embedding failed: {error}");
        }

        var result = await response.Content.ReadFromJsonAsync<OpenAIEmbeddingResponse>();
        var embedding = result?.Data?.FirstOrDefault()?.Embedding;
        if (embedding == null || embedding.Length == 0)
        {
            throw new InvalidOperationException("OpenAI returned empty embedding.");
        }
        return embedding;
    }

    private async Task<float[]> GenerateWithGooglePreset(LLMPreset preset, string text)
    {
        var apiKey = preset.ApiKey ?? throw new InvalidOperationException("API key required for Google AI");
        var embeddingUrl = preset.EmbeddingEndpointUrl ?? "https://generativelanguage.googleapis.com";
        var model = preset.EmbeddingModel ?? "text-embedding-004";

        var payload = new { content = new { parts = new[] { new { text } } } };
        var httpClient = new HttpClient();

        var response = await httpClient.PostAsJsonAsync(
            $"{embeddingUrl}/v1beta/models/{model}:embedContent?key={apiKey}", payload);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError("Google AI embedding failed: {Status} - {Error}", response.StatusCode, error);
            throw new InvalidOperationException($"Google AI embedding failed: {error}");
        }

        var result = await response.Content.ReadFromJsonAsync<GoogleAIEmbeddingResponse>();
        var embedding = result?.Embedding?.Values;
        if (embedding == null || embedding.Length == 0)
        {
            throw new InvalidOperationException("Google AI returned empty embedding.");
        }
        return embedding;
    }
}
