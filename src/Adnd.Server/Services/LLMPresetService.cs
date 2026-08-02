using Adnd.Server.Data;
using Adnd.Server.Dtos;
using Adnd.Server.Models;
using Adnd.Server.Services.Llm;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Services;

public interface ILLMPresetService
{
    Task<List<LLMPreset>> GetUserPresetsAsync(Guid userId);
    Task<LLMPreset> GetByIdAsync(Guid id, Guid userId);
    Task<LLMPreset> CreateAsync(Guid userId, CreateLLMPresetDto dto);
    Task<LLMPreset> UpdateAsync(Guid id, Guid userId, UpdateLLMPresetDto dto);
    Task DeleteAsync(Guid id, Guid userId);
    Task SetDefaultAsync(Guid id, Guid userId);
    Task<ProviderStatus> TestConnectionAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<string>> ListModelsAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<string>> QueryModelsAsync(QueryModelsDto dto, CancellationToken ct = default);
}

public class LLMPresetService(
    AppDbContext db,
    IApiKeyEncryptionService encryption,
    ILLMProviderFactory providerFactory,
    IOutboundUrlGuard urlGuard) : ILLMPresetService
{
    public async Task<List<LLMPreset>> GetUserPresetsAsync(Guid userId)
    {
        var presets = await db.LLMPresets
            .Where(p => p.UserId == userId)
            .OrderBy(p => p.Name)
            .ToListAsync();

        foreach (var preset in presets.Where(p => p.ApiKey is not null))
            preset.DecryptedApiKey = encryption.Decrypt(preset.ApiKey!);

        return presets;
    }

    public async Task<LLMPreset> GetByIdAsync(Guid id, Guid userId)
    {
        var preset = await db.LLMPresets
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId)
            ?? throw new KeyNotFoundException($"LLM preset {id} not found.");

        if (preset.ApiKey is not null)
            preset.DecryptedApiKey = encryption.Decrypt(preset.ApiKey);

        return preset;
    }

    public async Task<LLMPreset> CreateAsync(Guid userId, CreateLLMPresetDto dto)
    {
        var preset = new LLMPreset
        {
            UserId = userId,
            Name = dto.Name,
            ProviderType = dto.ProviderType,
            BaseModel = dto.BaseModel,
            EndpointUrl = dto.EndpointUrl,
            ApiKey = dto.ApiKey is not null ? encryption.Encrypt(dto.ApiKey) : null,
            Temperature = dto.Temperature,
            MaxTokens = dto.MaxTokens,
            TopP = dto.TopP,
            FrequencyPenalty = dto.FrequencyPenalty,
            PresencePenalty = dto.PresencePenalty,
            Stream = dto.Stream,
            TimeoutMs = dto.TimeoutMs,
            ReasoningEffort = dto.ReasoningEffort,
            EmbeddingModel = dto.EmbeddingModel,
            EmbeddingEndpointUrl = dto.EmbeddingEndpointUrl
        };

        db.LLMPresets.Add(preset);
        await db.SaveChangesAsync();

        // Honour the "set as default" toggle — it was accepted by the UI and silently dropped.
        if (dto.IsDefault)
            await SetDefaultAsync(preset.Id, userId);

        preset.DecryptedApiKey = dto.ApiKey;
        return preset;
    }

    public async Task<LLMPreset> UpdateAsync(Guid id, Guid userId, UpdateLLMPresetDto dto)
    {
        var preset = await db.LLMPresets
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId)
            ?? throw new KeyNotFoundException($"LLM preset {id} not found.");

        preset.Name = dto.Name;
        if (dto.ProviderType is not null) preset.ProviderType = dto.ProviderType;
        if (dto.BaseModel is not null) preset.BaseModel = dto.BaseModel;
        if (dto.EndpointUrl is not null) preset.EndpointUrl = dto.EndpointUrl;
        if (dto.ApiKey is not null) preset.ApiKey = encryption.Encrypt(dto.ApiKey);
        if (dto.Temperature.HasValue) preset.Temperature = dto.Temperature.Value;
        if (dto.MaxTokens.HasValue) preset.MaxTokens = dto.MaxTokens.Value;
        if (dto.TopP.HasValue) preset.TopP = dto.TopP.Value;
        if (dto.FrequencyPenalty.HasValue) preset.FrequencyPenalty = dto.FrequencyPenalty.Value;
        if (dto.PresencePenalty.HasValue) preset.PresencePenalty = dto.PresencePenalty.Value;
        if (dto.Stream.HasValue) preset.Stream = dto.Stream.Value;
        if (dto.TimeoutMs.HasValue) preset.TimeoutMs = dto.TimeoutMs.Value;
        if (dto.ReasoningEffort is not null) preset.ReasoningEffort = dto.ReasoningEffort;
        if (dto.EmbeddingModel is not null) preset.EmbeddingModel = dto.EmbeddingModel;
        if (dto.EmbeddingEndpointUrl is not null) preset.EmbeddingEndpointUrl = dto.EmbeddingEndpointUrl;
        preset.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync();

        if (dto.IsDefault == true)
            await SetDefaultAsync(preset.Id, userId);

        if (preset.ApiKey is not null)
            preset.DecryptedApiKey = encryption.Decrypt(preset.ApiKey);

        return preset;
    }

    public async Task DeleteAsync(Guid id, Guid userId)
    {
        var preset = await db.LLMPresets
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId)
            ?? throw new KeyNotFoundException($"LLM preset {id} not found.");

        db.LLMPresets.Remove(preset);
        await db.SaveChangesAsync();
    }

    public async Task SetDefaultAsync(Guid id, Guid userId)
    {
        var exists = await db.LLMPresets.AnyAsync(p => p.Id == id && p.UserId == userId);
        if (!exists)
            throw new KeyNotFoundException($"LLM preset {id} not found.");

        var userPresets = await db.LLMPresets
            .Where(p => p.UserId == userId)
            .ToListAsync();

        foreach (var preset in userPresets)
            preset.IsDefault = preset.Id == id;

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Actually probes the provider. This used to just check the row existed and return "ok",
    /// so the UI reported every misconfigured preset as reachable.
    /// </summary>
    public async Task<ProviderStatus> TestConnectionAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var preset = await LoadDecryptedAsync(id, userId);
        var provider = providerFactory.CreateFromPreset(preset);

        try
        {
            return await provider.GetStatusAsync(ct);
        }
        catch (Exception ex)
        {
            return new ProviderStatus(false, preset.BaseModel, ex.Message);
        }
    }

    public async Task<IReadOnlyList<string>> ListModelsAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var preset = await LoadDecryptedAsync(id, userId);
        var provider = providerFactory.CreateFromPreset(preset);
        return await provider.ListModelsAsync(ct);
    }

    public Task<IReadOnlyList<string>> QueryModelsAsync(QueryModelsDto dto, CancellationToken ct = default)
    {
        // The endpoint is fully caller-supplied, so it must be validated before the server
        // will fetch it — otherwise this is an SSRF primitive into the Docker network.
        urlGuard.EnsureAllowed(dto.EndpointUrl);

        var preset = new LLMPreset
        {
            ProviderType = dto.ProviderType,
            EndpointUrl = dto.EndpointUrl,
            DecryptedApiKey = dto.ApiKey,
            BaseModel = string.Empty,
        };
        var provider = providerFactory.CreateFromPreset(preset);
        return provider.ListModelsAsync(ct);
    }

    private async Task<LLMPreset> LoadDecryptedAsync(Guid id, Guid userId)
    {
        var preset = await db.LLMPresets
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId)
            ?? throw new KeyNotFoundException($"LLM preset {id} not found.");

        if (preset.ApiKey is not null)
            preset.DecryptedApiKey = encryption.Decrypt(preset.ApiKey);

        return preset;
    }
}
