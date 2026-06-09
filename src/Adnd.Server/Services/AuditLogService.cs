using Microsoft.EntityFrameworkCore;
using Adnd.Server.Data;
using Adnd.Server.Models;

namespace Adnd.Server.Services;

/// <summary>
/// Audit log entry for tracking admin actions.
/// </summary>
public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Action { get; set; } = string.Empty; // "CreateLLMPreset", "DeleteLLMPreset", "UpdateSystem", etc.
    public string? Entity { get; set; } // Entity type, e.g., "LLMPreset"
    public Guid? EntityId { get; set; }
    public string? Details { get; set; } // JSON details of the change
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public User? User { get; set; }
}

/// <summary>
/// Service for recording admin audit logs.
/// </summary>
public interface IAuditLogService
{
    Task LogAsync(Guid userId, string action, string? entity = null, Guid? entityId = null, string? details = null, string? ipAddress = null);
    Task<List<AuditLog>> GetUserLogsAsync(Guid userId, int limit = 50);
    Task DeleteOldLogsAsync(Guid before);
}

public class AuditLogService : IAuditLogService
{
    private readonly AppDbContext _context;
    private readonly ILogger<AuditLogService> _logger;

    public AuditLogService(AppDbContext context, ILogger<AuditLogService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task LogAsync(Guid userId, string action, string? entity = null, Guid? entityId = null, string? details = null, string? ipAddress = null)
    {
        var log = new AuditLog
        {
            UserId = userId,
            Action = action,
            Entity = entity,
            EntityId = entityId,
            Details = details,
            IpAddress = ipAddress
        };

        _context.AuditLogs.Add(log);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Audit: {Action} by user {UserId} on {Entity} {EntityId}", action, userId, entity, entityId);
    }

    public async Task<List<AuditLog>> GetUserLogsAsync(Guid userId, int limit = 50)
    {
        return await _context.AuditLogs
            .Where(l => l.UserId == userId)
            .OrderByDescending(l => l.CreatedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task DeleteOldLogsAsync(Guid before)
    {
        var cutoff = DateTime.UtcNow.AddMonths(-3);
        var oldLogs = await _context.AuditLogs
            .Where(l => l.CreatedAt < cutoff)
            .ToListAsync();

        _context.AuditLogs.RemoveRange(oldLogs);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Deleted {Count} old audit logs (before {Cutoff})", oldLogs.Count, cutoff);
    }
}
