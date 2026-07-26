using Adnd.Server.Data;
using Adnd.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Services;

public interface IPlayerManagementService
{
    Task<Player?> GetPlayerAsync(Guid gameId, Guid userId);
    Task<List<Player>> GetPlayersWithUsersAsync(Guid gameId);
}

public class PlayerManagementService(AppDbContext db) : IPlayerManagementService
{
    public async Task<Player?> GetPlayerAsync(Guid gameId, Guid userId)
    {
        return await db.Players
            .FirstOrDefaultAsync(p => p.GameId == gameId && p.UserId == userId);
    }

    public async Task<List<Player>> GetPlayersWithUsersAsync(Guid gameId)
    {
        return await db.Players
            .Include(p => p.User)
            .Where(p => p.GameId == gameId)
            .OrderBy(p => p.Role)
            .ThenBy(p => p.CharacterName)
            .ToListAsync();
    }
}
