using Adnd.Server.Data;
using Adnd.Server.Events;
using Adnd.Server.Handlers;
using Adnd.Server.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MassTransit;
using System.Collections.Concurrent;
using System.Linq;
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
    private readonly Dictionary<string, List<(string handlerId, Type eventType, Type handlerType)>> _handlerMap;
    private bool _handlersRegistered = false;
    private readonly object _registerLock = new();

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
        _handlerMap = new();
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

        // Publish via MassTransit (parallel run — also published by MassTransit consumers)
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

                // Publish succeeded — dispatch to handlers and exit retry loop
                var dispatched = await DispatchToHandlers(record, ct);
                return dispatched;
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

        _logger.LogInformation("EventBusWorker started with {HandlerCount} registered handlers", _handlerMap.Count);
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

    /// <summary>
    /// Thread-safe lazy initialization of event handlers.
    /// Called before first dispatch to avoid constructor deadlocks during DI resolution.
    /// </summary>
    private void EnsureHandlersRegistered()
    {
        if (_handlersRegistered)
            return;

        lock (_registerLock)
        {
            if (_handlersRegistered)
                return;

            RegisterHandlers();
            _handlersRegistered = true;
        }
    }

    private void RegisterHandlers()
    {
        // Only scan the Handlers namespace to avoid loading all types in the assembly
        // (which can trigger static constructors / DI resolution that cause deadlocks)
        var handlerAssembly = typeof(GameLifecycleHandler).Assembly;
        var iEventHandlerType = typeof(IEventHandler<>);

        foreach (var type in handlerAssembly.GetTypes()
            .Where(t => t.Namespace == "Adnd.Server.Handlers" &&
                        !t.IsAbstract && !t.IsInterface &&
                        t.GetInterfaces().Any(i => i.IsGenericType &&
                                                   i.GetGenericTypeDefinition() == iEventHandlerType)
        ))
        {
            // Register for ALL IEventHandler<T> interfaces this type implements
            var handlerInterfaces = type.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == iEventHandlerType);

            foreach (var handlerInterface in handlerInterfaces)
            {
                var eventType = handlerInterface.GetGenericArguments()[0];
                var key = eventType.FullName!;

                var handlerId = Guid.NewGuid().ToString();
                if (!_handlerMap.TryGetValue(key, out var list))
                {
                    list = new List<(string, Type, Type)>();
                    _handlerMap[key] = list;
                }
                list.Add((handlerId, eventType, type));
            }
        }

        _logger.LogInformation("Registered {Count} event handler types", _handlerMap.Count);
        foreach (var kvp in _handlerMap.OrderBy(k => k.Key))
        {
            _logger.LogInformation("  Handler for {EventType}: {Count} handlers [{Handlers}]",
                kvp.Key, kvp.Value.Count,
                string.Join(", ", kvp.Value.Select(h => h.handlerType.Name)));
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
    /// Dispatch a single event record to RabbitMQ (via MassTransit) and then to handlers.
    /// Used by startup replay and admin push-pending. Not used for runtime publishing.
    /// Returns true if the event was successfully dispatched to all handlers.
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

        // Dispatch to registered handlers
        return await DispatchToHandlers(record, ct);
    }

    private async Task<bool> DispatchToHandlers(EventRecord record, CancellationToken ct)
    {
        using var dbScope = _serviceProvider.CreateScope();
        var context = dbScope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Ensure handlers are registered (lazy init to avoid constructor deadlocks)
        EnsureHandlersRegistered();

        if (!_handlerMap.TryGetValue(record.EventType, out var handlers))
        {
            _logger.LogDebug("[EVENT] NoHandlerForType | EventType={EventType} | EventId={EventId}",
                record.EventType, record.Id);
            return true; // No handlers = not an error
        }

        var success = true;

        // Deserialize the payload
        IGameEvent? evt;
        try
        {
            evt = (IGameEvent?)JsonSerializer.Deserialize(record.Payload, handlers[0].eventType);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "[EVENT] FailedToDeserialize | EventType={EventType} | EventId={EventId}",
                record.EventType, record.Id);
            return false;
        }

        if (evt == null)
        {
            _logger.LogError("[EVENT] NullDeserializedEvent | EventType={EventType} | EventId={EventId}",
                record.EventType, record.Id);
            return false;
        }

        // Invoke each handler
        _logger.LogInformation("[EVENT] Dispatching {Count} handlers for {EventType} | EventId={EventId}",
            handlers.Count, record.EventType, record.Id);
        foreach (var (handlerId, _, handlerType) in handlers)
        {
            try
            {
                // Create handler via ActivatorUtilities to avoid DI circular dependencies
                // (handlers depend on IAgentBus which depends on IEventBus which is EventBusWorker)
                using var scope = _serviceProvider.CreateScope();
                var handlerInstance = ActivatorUtilities.CreateInstance(scope.ServiceProvider, handlerType);

                // Get the HandleAsync method that matches the event type
                // Handlers have multiple overloads (one per event type), so we must match by parameter
                var handleMethod = handlerType.GetMethods()
                    .FirstOrDefault(m => m.Name == "HandleAsync" &&
                                         m.GetParameters().Length >= 1 &&
                                         m.GetParameters()[0].ParameterType == handlers[0].eventType);
                _logger.LogInformation("[EVENT] HandleMethod | Handler={Handler} | Found={Found} | EventType={EventType}",
                    handlerType.Name, handleMethod != null, handlers[0].eventType.FullName);
                if (handleMethod == null)
                {
                    _logger.LogError("[EVENT] NoHandleAsync | Handler={HandlerType} | EventId={EventId} | EventType={EventType}",
                        handlerType.Name, record.Id, handlers[0].eventType.FullName);
                    success = false;
                    continue;
                }

                // Invoke the handler
                var task = (Task)handleMethod.Invoke(handlerInstance, new object[] { evt, ct })!;
                await task;

                _logger.LogDebug("[EVENT] Handled | Handler={Handler} | EventType={EventType} | EventId={EventId}",
                    handlerType.Name, record.EventType, record.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[EVENT] HandlerException | Handler={Handler} | EventType={EventType} | EventId={EventId}",
                    handlerType.Name, record.EventType, record.Id);
                success = false;
            }
        }

        // Update status based on dispatch result
        if (success)
        {
            record.Status = EventStatus.Acknowledged;
            record.AckedAt = DateTime.UtcNow;
        }
        else
        {
            record.Status = EventStatus.Failed;
            record.Error = "One or more handlers failed";
        }

        await context.SaveChangesAsync(ct);

        return success;
    }

    public override async Task StopAsync(CancellationToken ct)
    {
        // MassTransit handles connection lifecycle — no manual cleanup needed
        await base.StopAsync(ct);
    }
}
