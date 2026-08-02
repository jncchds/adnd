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

        // Only reuse the pointed-at session if it is still open. A closed CurrentSession
        // used to be handed out anyway, so hub messages landed in a closed session while
        // GM narration went to a different, arbitrarily-chosen active one.
        if (game.CurrentSession is { Status: GameSessionStatus.Active })
            return game.CurrentSession;

        var existingActive = await db.GameSessions
            .Where(s => s.GameId == gameId && s.Status == GameSessionStatus.Active)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync();

        if (existingActive is not null)
        {
            game.CurrentSessionId = existingActive.Id;
            await db.SaveChangesAsync();
            return existingActive;
        }

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

        if (game is null) return null;
        if (game.CurrentSession is { Status: GameSessionStatus.Active })
            return game.CurrentSession;

        return await db.GameSessions
            .Where(s => s.GameId == gameId && s.Status == GameSessionStatus.Active)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync();
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
