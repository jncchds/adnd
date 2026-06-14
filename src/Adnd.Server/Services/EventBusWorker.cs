using Adnd.Server.Data;
using Adnd.Server.Events;
using Adnd.Server.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MassTransit;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Adnd.Server.Services;

/// <summary>
/// Background service that implements IEventBus with RabbitMQ delivery.
/// On startup, replays pending EventRecords. On publish, persists to DB
/// and pushes to RabbitMQ with bounded retry (3 × 500ms).
/// No polling — event-driven via RabbitMQ consumer.
/// </summary>
public class EventBusWorker : BackgroundService, IEventBus
{
    private readonly ILogger<EventBusWorker> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly IPublishEndpoint _publishEndpoint;

    public EventBusWorker(
        ILogger<EventBusWorker> logger,
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        IPublishEndpoint publishEndpoint)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _publishEndpoint = publishEndpoint;
    }

    // ==================== IEventBus Implementation ====================

    public async Task PublishAsync<TEvent>(TEvent evt, CancellationToken ct = default) where TEvent : IGameEvent
    {
        // Persist to DB (fire-and-forget)
        var record = new EventRecord
        {
            Id = Guid.NewGuid(),
            GameId = evt.GameId,
            EventType = typeof(TEvent).FullName!,
            Payload = JsonSerializer.Serialize(evt),
            Status = EventStatus.Pending,
            CorrelationId = Guid.NewGuid().ToString(),
            CreatedAt = DateTime.UtcNow
        };

        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        context.EventRecords.Add(record);
        await context.SaveChangesAsync(ct);

        _logger.LogInformation("[EVENT] Queued | GameId={GameId} | EventType={EventType} | EventId={EventId}",
            evt.GameId, typeof(TEvent).Name, record.Id);

        // Publish via MassTransit (MassTransit consumers handle dispatch)
        var publishSuccess = await PublishWithRetry(record, ct);

        if (publishSuccess)
        {
            // Lazy cleanup: delete old acknowledged records after each successful publish
            await CleanupOldAcknowledgedRecords(context, ct);
        }
        else
        {
            // All retries failed — event stays in DB as Pending
            // Will be recovered on next app restart via ReplayPendingEvents
            _logger.LogWarning("[EVENT] PublishFailedAfterRetry | GameId={GameId} | EventType={EventType} | EventId={EventId} | Stays in DB as Pending",
                evt.GameId, typeof(TEvent).Name, record.Id);
        }
    }

    /// <summary>
    /// Publish an event to RabbitMQ with bounded retry (3 attempts, 500ms between each).
    /// Uses MassTransit IPublishEndpoint — no manual RabbitMQ.Client code.
    /// Returns true if successful, false if all retries failed.
    /// </summary>
    private async Task<bool> PublishWithRetry(EventRecord record, CancellationToken ct)
    {
        const int maxRetries = 3;
        const int retryDelayMs = 500;

        // Deserialize the payload back to the event type
        IGameEvent? evt;
        try
        {
            evt = (IGameEvent?)JsonSerializer.Deserialize(record.Payload, Type.GetType(record.EventType)!);
        }
        catch
        {
            _logger.LogError("[EVENT] FailedToDeserializeForPublish | EventType={EventType} | EventId={EventId}",
                record.EventType, record.Id);
            return false;
        }

        if (evt == null)
        {
            _logger.LogError("[EVENT] NullDeserializedEventForPublish | EventType={EventType} | EventId={EventId}",
                record.EventType, record.Id);
            return false;
        }

        // Retry loop: only handle RabbitMQ publish, NOT dispatch
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                await _publishEndpoint.Publish(evt, ct);

                // Update status in DB
                using var dbScope = _serviceProvider.CreateScope();
                var dbContext = dbScope.ServiceProvider.GetRequiredService<AppDbContext>();
                var updated = await dbScope.ServiceProvider.GetRequiredService<AppDbContext>()
                    .EventRecords.FindAsync(record.Id);
                if (updated != null)
                {
                    updated.Status = EventStatus.Published;
                    updated.PublishedAt = DateTime.UtcNow;
                    await dbScope.ServiceProvider.GetRequiredService<AppDbContext>().SaveChangesAsync(ct);
                }

                _logger.LogInformation("[EVENT] PublishedToRabbitMQ | GameId={GameId} | EventType={EventType} | EventId={EventId}",
                    record.GameId, record.EventType, record.Id);

                // Publish succeeded — MassTransit consumers handle dispatch
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[EVENT] PublishAttempt{Attempt}Failed | GameId={GameId} | EventType={EventType} | EventId={EventId}",
                    attempt + 1, record.GameId, record.EventType, record.Id);

                if (attempt < maxRetries - 1)
                {
                    await Task.Delay(retryDelayMs, ct);
                }
            }
        }

        // Publish never succeeded
        return false;
    }

    /// <summary>
    /// Push all pending events for a specific game to RabbitMQ.
    /// Called by the admin UI "Push Pending Events" button.
    /// </summary>
    public async Task<int> PushPendingEventsAsync(Guid gameId)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var pending = await context.EventRecords
            .Where(e => e.GameId == gameId && e.Status == EventStatus.Pending)
            .OrderBy(e => e.CreatedAt)
            .ToListAsync();

        int pushed = 0;
        foreach (var record in pending)
        {
            try
            {
                var success = await PublishWithRetry(record, CancellationToken.None);
                if (success) pushed++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[EVENT] FailedToPushPending | GameId={GameId} | EventId={EventId}", gameId, record.Id);
            }
        }

        _logger.LogInformation("[EVENT] PushPendingEvents | GameId={GameId} | Pushed={Pushed} | TotalPending={Total}",
            gameId, pushed, pending.Count);

        return pushed;
    }

    /// <summary>
    /// Get count of pending events for a specific game.
    /// Called by the admin UI to display pending count.
    /// </summary>
    public async Task<int> GetPendingEventsCountAsync(Guid gameId)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await context.EventRecords
            .CountAsync(e => e.GameId == gameId && e.Status == EventStatus.Pending);
    }

    /// <summary>
    /// Lazy cleanup: delete acknowledged EventRecords older than 7 days.
    /// Called after each successful publish to keep the table bounded.
    /// </summary>
    private async Task CleanupOldAcknowledgedRecords(AppDbContext context, CancellationToken ct)
    {
        try
        {
            var cutoff = DateTime.UtcNow.AddDays(-7);
            var deleted = await context.EventRecords
                .Where(e => e.Status == EventStatus.Acknowledged && e.AckedAt < cutoff)
                .ExecuteDeleteAsync(ct);

            if (deleted > 0)
            {
                _logger.LogDebug("[EVENT] CleanupOldRecords | Deleted={Count} | Cutoff={Cutoff}", deleted, cutoff);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[EVENT] CleanupFailed");
        }
    }

    public override async Task StartAsync(CancellationToken ct)
    {
        await base.StartAsync(ct);

        // Replay pending events from previous run
        await ReplayPendingEvents(ct);

        // Recover active game agents
        try
        {
            await RecoverGameAgentsAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to recover game agents during startup");
        }

        _logger.LogInformation("EventBusWorker started — MassTransit consumers handle event dispatch");
    }

    /// <summary>
    /// Recover active game agents from the database.
    /// Called after RabbitMQ is connected to ensure agents can establish their consumers.
    /// </summary>
    private async Task RecoverGameAgentsAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var agentManager = scope.ServiceProvider.GetRequiredService<IGameAgentManager>();
        var logger = _serviceProvider.GetRequiredService<ILogger<EventBusWorker>>();

        var activeGames = await context.Games
            .Where(g => g.Status == Models.GameStatus.Active && g.GMStatus == Models.GMStatus.Running)
            .Select(g => g.Id)
            .ToListAsync(ct);

        logger.LogInformation("Recovering {Count} active game agents", activeGames.Count);

        foreach (var gameId in activeGames)
        {
            try
            {
                var agent = agentManager.GetOrCreate(gameId);
                await agent.StartAsync(gameId, Guid.Empty);
                logger.LogInformation("Recovered game agent for game {GameId}", gameId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to recover game agent for game {GameId}", gameId);
            }
        }
    }

    private async Task ReplayPendingEvents(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var pending = await context.EventRecords
            .Where(e => e.Status == EventStatus.Pending)
            .ToListAsync(ct);

        _logger.LogInformation("Replaying {Count} pending events from previous run", pending.Count);

        foreach (var record in pending)
        {
            await DispatchEvent(record, ct);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // No polling loop — event-driven via RabbitMQ consumer.
        // The service runs only to keep the process alive and handle shutdown.
        await Task.CompletedTask;
    }

    /// <summary>
    /// Publish a single event record to RabbitMQ (via MassTransit).
    /// Used by startup replay and admin push-pending. Not used for runtime publishing.
    /// Returns true if the event was successfully published.
    /// </summary>
    public async Task<bool> DispatchEvent(EventRecord record, CancellationToken ct)
    {
        // Update status in DB using a new scope
        using var dbScope = _serviceProvider.CreateScope();
        var context = dbScope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Publish via MassTransit
        IGameEvent? evt;
        try
        {
            evt = (IGameEvent?)JsonSerializer.Deserialize(record.Payload, Type.GetType(record.EventType)!);
        }
        catch
        {
            _logger.LogError("[EVENT] FailedToDeserializeForDispatch | EventType={EventType} | EventId={EventId}",
                record.EventType, record.Id);
            return false;
        }

        if (evt == null)
        {
            _logger.LogError("[EVENT] NullDeserializedEventForDispatch | EventType={EventType} | EventId={EventId}",
                record.EventType, record.Id);
            return false;
        }

        try
        {
            await _publishEndpoint.Publish(evt, ct);

            record.Status = EventStatus.Published;
            record.PublishedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(ct);

            _logger.LogInformation("[EVENT] PublishedToRabbitMQ | GameId={GameId} | EventType={EventType} | EventId={EventId}",
                record.GameId, record.EventType, record.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[EVENT] FailedToPublishToRabbitMQ | GameId={GameId} | EventType={EventType} | EventId={EventId}",
                record.GameId, record.EventType, record.Id);
            return false;
        }

        // MassTransit consumers handle dispatch — no reflection needed
        return true;
    }

    public override async Task StopAsync(CancellationToken ct)
    {
        // MassTransit handles connection lifecycle — no manual cleanup needed
        await base.StopAsync(ct);
    }
}
