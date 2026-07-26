using Adnd.Server.Data;
using Adnd.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Services;

public interface IGameAuthorizationService
{
    Task<Player> RequirePlayerAsync(Guid gameId, Guid userId);
    Task<Player> RequireCreatorAsync(Guid gameId, Guid userId);
    Task<Player?> TryGetPlayerAsync(Guid gameId, Guid userId);
    Task<bool> IsCreatorAsync(Guid gameId, Guid userId);
}

public class GameAuthorizationService(AppDbContext db) : IGameAuthorizationService
{
    public async Task<Player?> TryGetPlayerAsync(Guid gameId, Guid userId)
    {
        return await db.Players
            .FirstOrDefaultAsync(p => p.GameId == gameId && p.UserId == userId);
    }

    public async Task<Player> RequirePlayerAsync(Guid gameId, Guid userId)
    {
        return await TryGetPlayerAsync(gameId, userId)
            ?? throw new UnauthorizedAccessException("User is not a player in this game.");
    }

    public async Task<Player> RequireCreatorAsync(Guid gameId, Guid userId)
    {
        var player = await RequirePlayerAsync(gameId, userId);
        if (player.Role != PlayerRole.Creator)
            throw new UnauthorizedAccessException("Only the creator can perform this action.");
        return player;
    }

    public async Task<bool> IsCreatorAsync(Guid gameId, Guid userId)
    {
        var player = await TryGetPlayerAsync(gameId, userId);
        return player?.Role == PlayerRole.Creator;
    }
}
