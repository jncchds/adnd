using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Adnd.Server.Models;

namespace Adnd.Server.Services;

/// <summary>
/// Background service that periodically checks for disconnected players.
/// Runs every 30 seconds and marks players as disconnected if they have no active SignalR connection.
/// </summary>
public class PlayerDisconnectDetector : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PlayerDisconnectDetector> _logger;
    private readonly TimeSpan _interval;
    private readonly TimeSpan _timeout;

    public PlayerDisconnectDetector(
        IServiceProvider serviceProvider,
        ILogger<PlayerDisconnectDetector> logger,
        TimeSpan? interval = null,
        TimeSpan? timeout = null)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _interval = interval ?? TimeSpan.FromSeconds(30);
        _timeout = timeout ?? TimeSpan.FromSeconds(60);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Player disconnect detector started. Interval: {Interval}, Timeout: {Timeout}",
            _interval, _timeout);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<Adnd.Server.Data.AppDbContext>();
                var playerConnections = GetPlayerConnections();

                var stalePlayers = await context.Players
                    .Where(p => p.Status == PlayerStatus.Active)
                    .ToListAsync(stoppingToken);

                foreach (var player in stalePlayers)
                {
                    var connectionId = playerConnections.GetValueOrDefault(player.Id.ToString());
                    if (connectionId == null)
                    {
                        player.Status = PlayerStatus.Disconnected;
                        player.LeftAt = DateTime.UtcNow;
                    }
                }

                await context.SaveChangesAsync(stoppingToken);

                var disconnectedCount = stalePlayers.Count(p => p.Status == PlayerStatus.Disconnected && p.LeftAt.HasValue);
                if (disconnectedCount > 0)
                {
                    _logger.LogInformation("Marked {Count} players as disconnected", disconnectedCount);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in player disconnect detector");
            }

            await Task.Delay(_interval, stoppingToken);
        }

        _logger.LogInformation("Player disconnect detector stopped");
    }

    /// <summary>
    /// Gets the current player-to-connection mapping from GameHub.
    /// In production, this should use a distributed cache or Redis instead.
    /// </summary>
    private ConcurrentDictionary<string, string> GetPlayerConnections()
    {
        // Access the static _playerConnections dictionary from GameHub
        // In production, replace with a distributed cache
        var hubType = typeof(Adnd.Server.Hubs.GameHub);
        var field = hubType.GetField("_playerConnections", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
        return field?.GetValue(null) as ConcurrentDictionary<string, string> ?? new ConcurrentDictionary<string, string>();
    }
}
