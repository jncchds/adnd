using Microsoft.EntityFrameworkCore;
using Adnd.Server.Data;
using Adnd.Server.Models;

namespace Adnd.Server.Services;

/// <summary>
/// Game access authorization. Replaces duplicated HasAccess() methods.
/// </summary>
public interface IGameAuthorizationService
{
    Task<bool> HasAccessAsync(AppDbContext context, Guid gameId, Guid userId);
    Task<bool> IsCreatorAsync(AppDbContext context, Guid gameId, Guid userId);
    /// <summary>
    /// True if the user is the creator OR the GM agent is running.
    /// Used for admin-level actions (sway, pause, etc.).
    /// </summary>
    Task<bool> CanAdminAsync(AppDbContext context, Guid gameId, Guid userId);
    /// <summary>
    /// True if the user has GM-level access (Creator or active GM role).
    /// Used for session notes, prompt templates, and other GM-only features.
    /// </summary>
    Task<bool> HasGmRoleAsync(AppDbContext context, Guid gameId, Guid userId);
}

public class GameAuthorizationService : IGameAuthorizationService
{
    public async Task<bool> HasAccessAsync(AppDbContext context, Guid gameId, Guid userId)
    {
        var game = await context.Games
            .Include(g => g.Players)
            .FirstOrDefaultAsync(g => g.Id == gameId);

        if (game == null) return false;
        return game.CreatorId == userId
            || game.Players.Any(p => p.UserId == userId && p.Status == PlayerStatus.Active);
    }

    public Task<bool> IsCreatorAsync(AppDbContext context, Guid gameId, Guid userId)
        => context.Games.AnyAsync(g => g.Id == gameId && g.CreatorId == userId);

    public async Task<bool> CanAdminAsync(AppDbContext context, Guid gameId, Guid userId)
    {
        var game = await context.Games
            .FirstOrDefaultAsync(g => g.Id == gameId);
        if (game == null) return false;
        // Creator can always admin. Also allow admin if GM agent is running.
        if (game.CreatorId == userId) return true;
        return game.GMStatus == GMStatus.Running;
    }

    public async Task<bool> HasGmRoleAsync(AppDbContext context, Guid gameId, Guid userId)
    {
        var game = await context.Games
            .Include(g => g.Players)
            .FirstOrDefaultAsync(g => g.Id == gameId);
        if (game == null) return false;
        // Creator always has GM role
        if (game.CreatorId == userId) return true;
        // Active players also have GM access for session notes/templates
        return game.Players.Any(p => p.UserId == userId && p.Status == PlayerStatus.Active && p.Role == PlayerRole.Player);
    }
}
