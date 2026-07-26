using Adnd.Server.Data;
using Adnd.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Services;

public interface ISessionManagementService
{
    Task<GameSession> GetOrCreateCurrentSessionAsync(Guid gameId);
    Task<GameSession?> GetCurrentSessionAsync(Guid gameId);
    Task CloseSessionAsync(Guid sessionId);
}

public class SessionManagementService(AppDbContext db) : ISessionManagementService
{
    public async Task<GameSession> GetOrCreateCurrentSessionAsync(Guid gameId)
    {
        var game = await db.Games
            .Include(g => g.CurrentSession)
            .FirstOrDefaultAsync(g => g.Id == gameId)
            ?? throw new KeyNotFoundException($"Game {gameId} not found.");

        if (game.CurrentSession is not null)
            return game.CurrentSession;

        var session = new GameSession
        {
            GameId = gameId,
            Title = $"Session {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm}",
            Status = GameSessionStatus.Active
        };

        db.GameSessions.Add(session);
        game.CurrentSessionId = session.Id;
        await db.SaveChangesAsync();

        return session;
    }

    public async Task<GameSession?> GetCurrentSessionAsync(Guid gameId)
    {
        var game = await db.Games
            .Include(g => g.CurrentSession)
            .FirstOrDefaultAsync(g => g.Id == gameId);

        return game?.CurrentSession;
    }

    public async Task CloseSessionAsync(Guid sessionId)
    {
        var session = await db.GameSessions.FindAsync(sessionId)
            ?? throw new KeyNotFoundException($"Session {sessionId} not found.");

        session.Status = GameSessionStatus.Closed;
        session.ClosedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
    }
}
