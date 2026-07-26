using System.Collections.Concurrent;
using Adnd.Server.Data;
using Adnd.Server.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Adnd.Server.Agent;

public class GameAgent(Guid gameId, IServiceProvider sp)
{
    private bool _isPaused;

    public Guid GameId => gameId;

    public async Task StartAsync()
    {
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var game = await db.Games.FindAsync(gameId);
        if (game == null) return;
        if (game.Status == GameStatus.Draft || game.Status == GameStatus.Starting)
        {
            game.Status = GameStatus.Starting;
            game.GMStatus = GMStatus.Running;
            await db.SaveChangesAsync();
        }
    }

    public async Task PauseAsync()
    {
        _isPaused = true;
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var game = await db.Games.FindAsync(gameId);
        if (game != null) { game.GMStatus = GMStatus.Paused; await db.SaveChangesAsync(); }
    }

    public async Task ResumeAsync()
    {
        _isPaused = false;
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var game = await db.Games.FindAsync(gameId);
        if (game != null) { game.GMStatus = GMStatus.Running; await db.SaveChangesAsync(); }
    }

    public bool IsPaused => _isPaused;
}

public interface IGameAgentManager
{
    Task<GameAgent> GetOrCreateAsync(Guid gameId);
    Task StartAllActiveGamesAsync();
}

public class GameAgentManager(IServiceProvider sp, ILogger<GameAgentManager> logger) : IGameAgentManager, IHostedService
{
    private readonly ConcurrentDictionary<Guid, GameAgent> _agents = new();

    public Task<GameAgent> GetOrCreateAsync(Guid gameId)
    {
        var agent = _agents.GetOrAdd(gameId, id => new GameAgent(id, sp));
        return Task.FromResult(agent);
    }

    public async Task StartAllActiveGamesAsync()
    {
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var activeGames = await db.Games
            .Where(g => g.Status == GameStatus.Active && g.GMStatus == GMStatus.Running)
            .Select(g => g.Id)
            .ToListAsync();
        foreach (var id in activeGames)
        {
            var agent = await GetOrCreateAsync(id);
            logger.LogInformation("Recovered agent for game {GameId}", id);
            _ = agent;
        }
    }

    public async Task StartAsync(CancellationToken ct) => await StartAllActiveGamesAsync();
    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
