using Microsoft.EntityFrameworkCore;
using Adnd.Server.Data;
using Adnd.Server.Models;

namespace Adnd.Server.Services;

/// <summary>
/// Service for prompt template CRUD operations.
/// Templates allow GMs to customize LLM system prompts per game.
/// </summary>
public interface IPromptTemplateService
{
    Task<List<PromptTemplateResponse>> GetTemplatesAsync(Guid gameId);
    Task<PromptTemplateResponse> CreateTemplateAsync(Guid gameId, Guid userId, string name, string type, string prompt);
    Task<PromptTemplateResponse> UpdateTemplateAsync(Guid gameId, Guid templateId, Guid updaterId, string name, string type, string prompt, bool? isActive, bool? isDefault);
    Task DeleteTemplateAsync(Guid gameId, Guid templateId, Guid deleterId);
    Task<PromptTemplateResponse?> GetDefaultTemplateAsync(Guid gameId, string type);
}

public class PromptTemplateService : IPromptTemplateService
{
    private readonly AppDbContext _context;
    private readonly ILogger<PromptTemplateService> _logger;

    public PromptTemplateService(AppDbContext context, ILogger<PromptTemplateService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<PromptTemplateResponse>> GetTemplatesAsync(Guid gameId)
    {
        return (await _context.PromptTemplates
            .Where(pt => pt.GameId == gameId)
            .OrderByDescending(pt => pt.IsDefault ? 0 : 1)
            .ThenByDescending(pt => pt.UpdatedAt ?? pt.CreatedAt)
            .Select(pt => new PromptTemplateResponse
            {
                Id = pt.Id,
                GameId = pt.GameId,
                UserId = pt.UserId,
                UserName = pt.User != null ? (pt.User.DisplayName ?? pt.User.Email ?? "Unknown") : "Unknown",
                Name = pt.Name,
                Type = pt.Type,
                Prompt = pt.Prompt,
                IsActive = pt.IsActive,
                IsDefault = pt.IsDefault,
                CreatedAt = pt.CreatedAt,
                UpdatedAt = pt.UpdatedAt
            })
            .ToListAsync());
    }

    public async Task<PromptTemplateResponse> CreateTemplateAsync(Guid gameId, Guid userId, string name, string type, string prompt)
    {
        // If setting as default, unset existing defaults of same type
        if (prompt.Contains("__DEFAULT__") || prompt.StartsWith("__DEFAULT__"))
        {
            // No auto-default — caller handles this explicitly
        }

        var template = new PromptTemplate
        {
            GameId = gameId,
            UserId = userId,
            Name = name,
            Type = type,
            Prompt = prompt
        };

        _context.PromptTemplates.Add(template);
        await _context.SaveChangesAsync();

        return new PromptTemplateResponse
        {
            Id = template.Id,
            GameId = template.GameId,
            UserId = template.UserId,
            UserName = template.User?.DisplayName ?? template.User?.Email ?? "Unknown",
            Name = template.Name,
            Type = template.Type,
            Prompt = template.Prompt,
            IsActive = template.IsActive,
            IsDefault = template.IsDefault,
            CreatedAt = template.CreatedAt,
            UpdatedAt = template.UpdatedAt
        };
    }

    public async Task<PromptTemplateResponse> UpdateTemplateAsync(Guid gameId, Guid templateId, Guid updaterId, string name, string type, string prompt, bool? isActive, bool? isDefault)
    {
        var template = await _context.PromptTemplates
            .FirstOrDefaultAsync(pt => pt.Id == templateId && pt.GameId == gameId);

        if (template == null)
            throw new KeyNotFoundException("Prompt template not found.");

        template.Name = name;
        template.Type = type;
        template.Prompt = prompt;
        if (isActive.HasValue) template.IsActive = isActive.Value;
        template.IsDefault = isDefault ?? template.IsDefault;
        template.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return new PromptTemplateResponse
        {
            Id = template.Id,
            GameId = template.GameId,
            UserId = template.UserId,
            UserName = template.User != null ? (template.User.DisplayName ?? template.User.Email ?? "Unknown") : "Unknown",
            Name = template.Name,
            Type = template.Type,
            Prompt = template.Prompt,
            IsActive = template.IsActive,
            IsDefault = template.IsDefault,
            CreatedAt = template.CreatedAt,
            UpdatedAt = template.UpdatedAt
        };
    }

    public async Task DeleteTemplateAsync(Guid gameId, Guid templateId, Guid deleterId)
    {
        var template = await _context.PromptTemplates
            .FirstOrDefaultAsync(pt => pt.Id == templateId && pt.GameId == gameId);

        if (template == null)
            throw new KeyNotFoundException("Prompt template not found.");

        if (template.UserId != deleterId)
            throw new UnauthorizedAccessException("Only the creator can delete their templates.");

        _context.PromptTemplates.Remove(template);
        await _context.SaveChangesAsync();
    }

    public async Task<PromptTemplateResponse?> GetDefaultTemplateAsync(Guid gameId, string type)
    {
        var template = await _context.PromptTemplates
            .Where(pt => pt.GameId == gameId && pt.Type == type && pt.IsActive && pt.IsDefault)
            .OrderByDescending(pt => pt.UpdatedAt ?? pt.CreatedAt)
            .Select(pt => new PromptTemplateResponse
            {
                Id = pt.Id,
                GameId = pt.GameId,
                UserId = pt.UserId,
                UserName = pt.User != null ? (pt.User.DisplayName ?? pt.User.Email ?? "Unknown") : "Unknown",
                Name = pt.Name,
                Type = pt.Type,
                Prompt = pt.Prompt,
                IsActive = pt.IsActive,
                IsDefault = pt.IsDefault,
                CreatedAt = pt.CreatedAt,
                UpdatedAt = pt.UpdatedAt
            })
            .FirstOrDefaultAsync();

        return template;
    }
}

public class PromptTemplateResponse
{
    public Guid Id { get; set; }
    public Guid GameId { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsDefault { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
