using Microsoft.EntityFrameworkCore;
using Adnd.Server.Data;
using Adnd.Server.Models;

namespace Adnd.Server.Services;

/// <summary>
/// Service for user-wide game template CRUD operations.
/// Templates let creators save game configurations (system, LLM preset, language, plot seed, parameters)
/// and reuse them when creating new games.
/// </summary>
public interface IGameTemplateService
{
    /// <summary>Get all templates owned by the user, ordered by most recently updated.</summary>
    Task<List<GameTemplateResponse>> GetTemplatesAsync(Guid userId);

    /// <summary>Create a new game template from a game configuration.</summary>
    Task<GameTemplateResponse> CreateTemplateAsync(Guid userId, string name, string defaultName, string systemId,
        Guid? llmPresetId, string? llmPresetName, string language, string? plotSeed, string? gameParameters);

    /// <summary>Update an existing template.</summary>
    Task<GameTemplateResponse> UpdateTemplateAsync(Guid userId, Guid templateId, string name, string? defaultName,
        string systemId, Guid? llmPresetId, string? llmPresetName, string language, string? plotSeed, string? gameParameters);

    /// <summary>Delete a template (owner only).</summary>
    Task DeleteTemplateAsync(Guid userId, Guid templateId);

    /// <summary>Get a single template by ID (owner only).</summary>
    Task<GameTemplateResponse?> GetTemplateAsync(Guid userId, Guid templateId);
}

public class GameTemplateService : IGameTemplateService
{
    private readonly AppDbContext _context;
    private readonly ILogger<GameTemplateService> _logger;

    public GameTemplateService(AppDbContext context, ILogger<GameTemplateService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<GameTemplateResponse>> GetTemplatesAsync(Guid userId)
    {
        return (await _context.GameTemplates
            .Where(gt => gt.UserId == userId)
            .OrderByDescending(gt => gt.UpdatedAt)
            .Select(gt => new GameTemplateResponse
            {
                Id = gt.Id,
                Name = gt.Name,
                DefaultName = gt.DefaultName,
                SystemId = gt.SystemId,
                LLMPresetId = gt.LLMPresetId,
                LLMPresetName = gt.LLMPresetName,
                Language = gt.Language,
                PlotSeed = gt.PlotSeed,
                GameParameters = gt.GameParameters,
                CreatedAt = gt.CreatedAt,
                UpdatedAt = gt.UpdatedAt
            })
            .ToListAsync());
    }

    public async Task<GameTemplateResponse> CreateTemplateAsync(Guid userId, string name, string defaultName,
        string systemId, Guid? llmPresetId, string? llmPresetName, string language, string? plotSeed, string? gameParameters)
    {
        var template = new GameTemplate
        {
            UserId = userId,
            Name = name,
            DefaultName = defaultName,
            SystemId = systemId,
            LLMPresetId = llmPresetId,
            LLMPresetName = llmPresetName,
            Language = language,
            PlotSeed = plotSeed,
            GameParameters = gameParameters
        };

        _context.GameTemplates.Add(template);
        await _context.SaveChangesAsync();

        return new GameTemplateResponse
        {
            Id = template.Id,
            Name = template.Name,
            DefaultName = template.DefaultName,
            SystemId = template.SystemId,
            LLMPresetId = template.LLMPresetId,
            LLMPresetName = template.LLMPresetName,
            Language = template.Language,
            PlotSeed = template.PlotSeed,
            GameParameters = template.GameParameters,
            CreatedAt = template.CreatedAt,
            UpdatedAt = template.UpdatedAt
        };
    }

    public async Task<GameTemplateResponse> UpdateTemplateAsync(Guid userId, Guid templateId, string name,
        string? defaultName, string systemId, Guid? llmPresetId, string? llmPresetName, string language,
        string? plotSeed, string? gameParameters)
    {
        var template = await _context.GameTemplates
            .FirstOrDefaultAsync(gt => gt.Id == templateId && gt.UserId == userId);

        if (template == null)
            throw new KeyNotFoundException("Game template not found.");

        template.Name = name;
        template.DefaultName = defaultName;
        template.SystemId = systemId;
        template.LLMPresetId = llmPresetId;
        template.LLMPresetName = llmPresetName;
        template.Language = language;
        template.PlotSeed = plotSeed;
        template.GameParameters = gameParameters;
        template.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return new GameTemplateResponse
        {
            Id = template.Id,
            Name = template.Name,
            DefaultName = template.DefaultName,
            SystemId = template.SystemId,
            LLMPresetId = template.LLMPresetId,
            LLMPresetName = template.LLMPresetName,
            Language = template.Language,
            PlotSeed = template.PlotSeed,
            GameParameters = template.GameParameters,
            CreatedAt = template.CreatedAt,
            UpdatedAt = template.UpdatedAt
        };
    }

    public async Task DeleteTemplateAsync(Guid userId, Guid templateId)
    {
        var template = await _context.GameTemplates
            .FirstOrDefaultAsync(gt => gt.Id == templateId && gt.UserId == userId);

        if (template == null)
            throw new KeyNotFoundException("Game template not found.");

        _context.GameTemplates.Remove(template);
        await _context.SaveChangesAsync();
    }

    public async Task<GameTemplateResponse?> GetTemplateAsync(Guid userId, Guid templateId)
    {
        var template = await _context.GameTemplates
            .Where(gt => gt.Id == templateId && gt.UserId == userId)
            .Select(gt => new GameTemplateResponse
            {
                Id = gt.Id,
                Name = gt.Name,
                DefaultName = gt.DefaultName,
                SystemId = gt.SystemId,
                LLMPresetId = gt.LLMPresetId,
                LLMPresetName = gt.LLMPresetName,
                Language = gt.Language,
                PlotSeed = gt.PlotSeed,
                GameParameters = gt.GameParameters,
                CreatedAt = gt.CreatedAt,
                UpdatedAt = gt.UpdatedAt
            })
            .FirstOrDefaultAsync();

        return template;
    }
}

/// <summary>
/// Response DTO for game templates.
/// </summary>
public class GameTemplateResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? DefaultName { get; set; }
    public string SystemId { get; set; } = string.Empty;
    public Guid? LLMPresetId { get; set; }
    public string? LLMPresetName { get; set; }
    public string Language { get; set; } = string.Empty;
    public string? PlotSeed { get; set; }
    public string? GameParameters { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
