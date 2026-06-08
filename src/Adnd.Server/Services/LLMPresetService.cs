using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Net.Http;
using Adnd.Server.Data;
using Adnd.Server.Models;

namespace Adnd.Server.Services;

public interface ILLMPresetService
{
    Task<List<LLMPreset>> GetUserPresetsAsync(Guid userId);
    Task<LLMPreset?> GetPresetAsync(Guid userId, Guid presetId);
    Task<LLMPreset> CreatePresetAsync(Guid userId, CreateLLMPresetRequest request);
    Task<LLMPreset> UpdatePresetAsync(Guid userId, Guid presetId, UpdateLLMPresetRequest request);
    Task DeletePresetAsync(Guid userId, Guid presetId);
    Task<LLMPreset?> GetDefaultPresetAsync(Guid userId);
    Task SetDefaultPresetAsync(Guid userId, Guid presetId);
    Task<bool> TestConnectionAsync(Guid userId, Guid presetId);
    Task<List<string>> GetAvailableModelsAsync(string providerType, string? endpointUrl = null, string? apiKey = null);
}

public class LLMPresetService : ILLMPresetService
{
    private readonly AppDbContext _context;
    private readonly ILogger<LLMPresetService> _logger;

    public LLMPresetService(AppDbContext context, ILogger<LLMPresetService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<LLMPreset>> GetUserPresetsAsync(Guid userId)
    {
        return await _context.LLMPresets
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.IsDefault)
            .ThenBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<LLMPreset?> GetPresetAsync(Guid userId, Guid presetId)
    {
        return await _context.LLMPresets
            .FirstOrDefaultAsync(p => p.UserId == userId && p.Id == presetId);
    }

    public async Task<LLMPreset> CreatePresetAsync(Guid userId, CreateLLMPresetRequest request)
    {
        // If this is set as default, unset any existing default
        if (request.IsDefault)
        {
            var existingDefault = await _context.LLMPresets
                .FirstOrDefaultAsync(p => p.UserId == userId && p.IsDefault);
            if (existingDefault != null)
            {
                existingDefault.IsDefault = false;
                existingDefault.UpdatedAt = DateTime.UtcNow;
            }
        }

        var preset = new LLMPreset
        {
            UserId = userId,
            Name = request.Name,
            ProviderType = request.ProviderType,
            BaseModel = request.BaseModel,
            EndpointUrl = request.EndpointUrl,
            ApiKey = request.ApiKey,
            Temperature = request.Temperature,
            MaxTokens = request.MaxTokens,
            TopP = request.TopP,
            FrequencyPenalty = request.FrequencyPenalty,
            PresencePenalty = request.PresencePenalty,
            Stream = request.Stream,
            EmbeddingModel = request.EmbeddingModel,
            EmbeddingEndpointUrl = request.EmbeddingEndpointUrl,
            IsActive = request.IsActive,
            IsDefault = request.IsDefault,
            ExtraParams = request.ExtraParams,
            CreatedAt = DateTime.UtcNow
        };

        _context.LLMPresets.Add(preset);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created LLM preset '{PresetName}' for user {UserId}", preset.Name, userId);
        return preset;
    }

    public async Task<LLMPreset> UpdatePresetAsync(Guid userId, Guid presetId, UpdateLLMPresetRequest request)
    {
        var preset = await _context.LLMPresets
            .FirstOrDefaultAsync(p => p.UserId == userId && p.Id == presetId);

        if (preset == null)
            throw new KeyNotFoundException($"Preset {presetId} not found.");

        if (request.Name != null) preset.Name = request.Name;
        if (request.ProviderType != null) preset.ProviderType = request.ProviderType;
        if (request.BaseModel != null) preset.BaseModel = request.BaseModel;
        if (request.EndpointUrl != null) preset.EndpointUrl = request.EndpointUrl;
        if (request.ApiKey != null) preset.ApiKey = request.ApiKey;
        if (request.Temperature.HasValue) preset.Temperature = request.Temperature.Value;
        if (request.MaxTokens.HasValue) preset.MaxTokens = request.MaxTokens.Value;
        if (request.TopP.HasValue) preset.TopP = request.TopP.Value;
        if (request.FrequencyPenalty != null) preset.FrequencyPenalty = request.FrequencyPenalty;
        if (request.PresencePenalty != null) preset.PresencePenalty = request.PresencePenalty;
        if (request.Stream != null) preset.Stream = request.Stream.Value;
        if (request.EmbeddingModel != null) preset.EmbeddingModel = request.EmbeddingModel;
        if (request.EmbeddingEndpointUrl != null) preset.EmbeddingEndpointUrl = request.EmbeddingEndpointUrl;
        if (request.IsActive != null) preset.IsActive = request.IsActive.Value;
        if (request.ExtraParams != null) preset.ExtraParams = request.ExtraParams;

        // Handle default flag
        if (request.IsDefault.HasValue && request.IsDefault.Value)
        {
            var existingDefault = await _context.LLMPresets
                .FirstOrDefaultAsync(p => p.UserId == userId && p.Id != presetId && p.IsDefault);
            if (existingDefault != null)
            {
                existingDefault.IsDefault = false;
                existingDefault.UpdatedAt = DateTime.UtcNow;
            }
            preset.IsDefault = true;
        }
        else if (request.IsDefault == false)
        {
            preset.IsDefault = false;
        }

        preset.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Updated LLM preset '{PresetName}' for user {UserId}", preset.Name, userId);
        return preset;
    }

    public async Task DeletePresetAsync(Guid userId, Guid presetId)
    {
        var preset = await _context.LLMPresets
            .FirstOrDefaultAsync(p => p.UserId == userId && p.Id == presetId);

        if (preset == null)
            throw new KeyNotFoundException($"Preset {presetId} not found.");

        _context.LLMPresets.Remove(preset);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Deleted LLM preset '{PresetName}' for user {UserId}", preset.Name, userId);
    }

    public async Task<LLMPreset?> GetDefaultPresetAsync(Guid userId)
    {
        return await _context.LLMPresets
            .FirstOrDefaultAsync(p => p.UserId == userId && p.IsDefault && p.IsActive);
    }

    public async Task SetDefaultPresetAsync(Guid userId, Guid presetId)
    {
        var preset = await _context.LLMPresets
            .FirstOrDefaultAsync(p => p.UserId == userId && p.Id == presetId);

        if (preset == null)
            throw new KeyNotFoundException($"Preset {presetId} not found.");

        // Unset any existing default
        var existingDefault = await _context.LLMPresets
            .FirstOrDefaultAsync(p => p.UserId == userId && p.IsDefault);
        if (existingDefault != null)
        {
            existingDefault.IsDefault = false;
            existingDefault.UpdatedAt = DateTime.UtcNow;
        }

        preset.IsDefault = true;
        preset.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    public async Task<bool> TestConnectionAsync(Guid userId, Guid presetId)
    {
        var preset = await _context.LLMPresets
            .FirstOrDefaultAsync(p => p.UserId == userId && p.Id == presetId);

        if (preset == null)
            throw new KeyNotFoundException($"Preset {presetId} not found.");

        var provider = CreateProviderFromPreset(preset);
        return await provider.IsAvailableAsync();
    }

    public async Task<List<string>> GetAvailableModelsAsync(string providerType, string? endpointUrl = null, string? apiKey = null)
    {
        return providerType.ToLowerInvariant() switch
        {
            "ollama" => await GetOllamaModelsAsync(endpointUrl),
            "lmstudio" => await GetOpenAICompatibleModelsAsync(endpointUrl, apiKey),
            "openai" => await GetOpenAIModelsAsync(endpointUrl, apiKey),
            "google" => await GetGoogleModelsAsync(apiKey),
            _ => throw new InvalidOperationException($"Provider type '{providerType}' does not support model listing."),
        };
    }

    private static async Task<List<string>> GetOllamaModelsAsync(string? baseUrl)
    {
        var url = (baseUrl ?? "http://localhost:11434").TrimEnd('/') + "/api/tags";
        using var http = new HttpClient();
        http.Timeout = TimeSpan.FromSeconds(10);
        var response = await http.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var doc = await response.Content.ReadFromJsonAsync<OllamaTagsResponse>();
        return doc?.Models?.Select(m => m.Name).ToList() ?? new List<string>();
    }

    private static async Task<List<string>> GetOpenAICompatibleModelsAsync(string? baseUrl, string? apiKey)
    {
        var url = (baseUrl ?? "http://localhost:1234").TrimEnd('/') + "/v1/models";
        using var http = new HttpClient();
        http.Timeout = TimeSpan.FromSeconds(10);
        if (!string.IsNullOrEmpty(apiKey))
            http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
        var response = await http.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var doc = await response.Content.ReadFromJsonAsync<OpenAIModelsResponse>();
        return doc?.Data?.Select(m => m.Id).ToList() ?? new List<string>();
    }

    private static async Task<List<string>> GetOpenAIModelsAsync(string? baseUrl, string? apiKey)
    {
        var url = (baseUrl ?? "https://api.openai.com/v1").TrimEnd('/') + "/models";
        using var http = new HttpClient();
        http.Timeout = TimeSpan.FromSeconds(10);
        if (!string.IsNullOrEmpty(apiKey))
            http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
        var response = await http.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var doc = await response.Content.ReadFromJsonAsync<OpenAIModelsResponse>();
        return doc?.Data?.Select(m => m.Id).ToList() ?? new List<string>();
    }

    private static async Task<List<string>> GetGoogleModelsAsync(string? apiKey)
    {
        if (string.IsNullOrEmpty(apiKey))
            throw new InvalidOperationException("API key is required for Google AI Studio.");
        var url = $"https://generativelanguage.googleapis.com/v1beta/models?key={apiKey}";
        using var http = new HttpClient();
        http.Timeout = TimeSpan.FromSeconds(10);
        var response = await http.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var doc = await response.Content.ReadFromJsonAsync<GoogleModelsResponse>();
        return doc?.Models?.Select(m => m.Name.Replace("models/", "")).ToList() ?? new List<string>();
    }

    private ILLMProvider CreateProviderFromPreset(LLMPreset preset)
    {
        return preset.ProviderType switch
        {
            "ollama" => new OllamaLLMProviderFromPreset(preset),
            "lmstudio" => new LmStudioLLMProviderFromPreset(preset),
            "openai" => new OpenAILLMProviderFromPreset(preset),
            "google" => new GoogleAIStudioLLMProviderFromPreset(preset),
            _ => throw new ArgumentException($"Unknown provider type: {preset.ProviderType}")
        };
    }
}

// Request DTOs
public class CreateLLMPresetRequest
{
    public string Name { get; set; } = string.Empty;
    public string ProviderType { get; set; } = string.Empty;
    public string BaseModel { get; set; } = string.Empty;
    public string? EndpointUrl { get; set; }
    public string? ApiKey { get; set; }
    public float Temperature { get; set; } = 0.7f;
    public int MaxTokens { get; set; } = 2048;
    public float TopP { get; set; } = 0.9f;
    public float? FrequencyPenalty { get; set; }
    public float? PresencePenalty { get; set; }
    public bool Stream { get; set; } = false;
    public string? EmbeddingModel { get; set; }
    public string? EmbeddingEndpointUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDefault { get; set; } = false;
    public JsonElement? ExtraParams { get; set; }
}

public class UpdateLLMPresetRequest
{
    public string? Name { get; set; }
    public string? ProviderType { get; set; }
    public string? BaseModel { get; set; }
    public string? EndpointUrl { get; set; }
    public string? ApiKey { get; set; }
    public float? Temperature { get; set; }
    public int? MaxTokens { get; set; }
    public float? TopP { get; set; }
    public float? FrequencyPenalty { get; set; }
    public float? PresencePenalty { get; set; }
    public bool? Stream { get; set; }
    public string? EmbeddingModel { get; set; }
    public string? EmbeddingEndpointUrl { get; set; }
    public bool? IsActive { get; set; }
    public bool? IsDefault { get; set; }
    public JsonElement? ExtraParams { get; set; }
}

public class TestConnectionResponse
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? StatusMessage { get; set; }
}

// ==================== Model listing response types ====================

public class OllamaTagsResponse
{
    public List<OllamaModelInfo>? Models { get; set; }
}

public class OllamaModelInfo
{
    public string Name { get; set; } = string.Empty;
}

public class OpenAIModelsResponse
{
    public List<OpenAIModelInfo>? Data { get; set; }
}

public class OpenAIModelInfo
{
    public string Id { get; set; } = string.Empty;
}

public class GoogleModelsResponse
{
    public List<GoogleModelInfo>? Models { get; set; }
}

public class GoogleModelInfo
{
    public string Name { get; set; } = string.Empty;
}
