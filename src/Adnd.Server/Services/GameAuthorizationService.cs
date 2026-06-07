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
    Task<bool> IsGameMasterAsync(AppDbContext context, Guid gameId, Guid userId);
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

    public Task<bool> IsGameMasterAsync(AppDbContext context, Guid gameId, Guid userId)
        => context.Games.AnyAsync(g => g.Id == gameId && g.GameMasterId == userId);
}
