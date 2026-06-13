using Adnd.Server.Data;
using Adnd.Server.Events;
using Adnd.Server.Handlers;
using Adnd.Server.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Framing;
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
    private readonly Dictionary<string, List<(string handlerId, Type eventType, Type handlerType)>> _handlerMap;
    private IConnection? _rabbitMqConnection;
    private IModel? _channel;
    private readonly object _lock = new();
    private readonly ConcurrentDictionary<string, IModel> _channelsByGame = new();
    private readonly ConcurrentDictionary<string, IModel> _channelsByAgent = new();
    private bool _handlersRegistered = false;
    private readonly object _registerLock = new();

    public EventBusWorker(
        ILogger<EventBusWorker> logger,
        IServiceProvider serviceProvider,
        IConfiguration configuration)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _configuration = configuration;
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
    /// Returns true if successful, false if all retries failed.
    /// </summary>
    private async Task<bool> PublishWithRetry(EventRecord record, CancellationToken ct)
    {
        const int maxRetries = 3;
        const int retryDelayMs = 500;

        // Retry loop: only handle RabbitMQ publish, NOT dispatch
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                // Ensure RabbitMQ connection is alive
                lock (_lock)
                {
                    if (_channel == null || !_rabbitMqConnection?.IsOpen == true)
                    {
                        ConnectToRabbitMq();
                    }
                }

                var body = System.Text.Encoding.UTF8.GetBytes(record.Payload);
                var properties = _channel!.CreateBasicProperties();
                properties.Persistent = true;
                properties.CorrelationId = record.CorrelationId;
                properties.DeliveryMode = 2; // persistent

                _channel.BasicPublish(
                    exchange: "adnd.events",
                    routingKey: $"game.{record.GameId}",
                    mandatory: false,
                    basicProperties: properties,
                    body: body);

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

                // Publish succeeded — break out of retry loop
                break;
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

        // Dispatch outside retry loop — only dispatch once, after publish succeeds
        // (dispatching inside the loop would cause duplicate events on retry)
        if (record.Status == EventStatus.Published)
        {
            return await DispatchToHandlers(record, ct);
        }

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

    public void Subscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : IGameEvent
    {
        // Dynamic subscription — not used in current design (startup scan handles it)
        // Kept for future extensibility
    }

    public void Unsubscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : IGameEvent
    {
        // Dynamic unsubscription
    }

    public override async Task StartAsync(CancellationToken ct)
    {
        await base.StartAsync(ct);

        // Connect to RabbitMQ
        ConnectToRabbitMq();

        // Replay pending events from previous run
        await ReplayPendingEvents(ct);

        _logger.LogInformation("EventBusWorker started with {HandlerCount} registered handlers", _handlerMap.Count);
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

    private void ConnectToRabbitMq()
    {
        var host = _configuration["RabbitMq:Host"] ?? "localhost";
        var port = _configuration.GetValue<int>("RabbitMq:Port", 5672);
        var username = _configuration["RabbitMq:Username"] ?? "adnd";
        var password = _configuration["RabbitMq:Password"] ?? "adnd";
        var virtualHost = _configuration["RabbitMq:VirtualHost"] ?? "/adnd";

        var factory = new ConnectionFactory
        {
            HostName = host,
            Port = port,
            UserName = username,
            Password = password,
            VirtualHost = virtualHost
        };

        _rabbitMqConnection = factory.CreateConnection();
        _channel = _rabbitMqConnection.CreateModel();

        // Declare exchange
        _channel.ExchangeDeclare("adnd.events", ExchangeType.Direct, durable: true);

        _logger.LogInformation("Connected to RabbitMQ at {Host}:{Port}", host, port);
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
    /// Dispatch a single event record to RabbitMQ and then to handlers.
    /// Used by startup replay and admin push-pending. Not used for runtime publishing
    /// (that goes through PublishAsync with bounded retry).
    /// Returns true if the event was successfully dispatched to all handlers.
    /// </summary>
    public async Task<bool> DispatchEvent(EventRecord record, CancellationToken ct)
    {
        // Update status in DB using a new scope
        using var dbScope = _serviceProvider.CreateScope();
        var context = dbScope.ServiceProvider.GetRequiredService<AppDbContext>();

        lock (_lock)
        {
            if (_channel == null || !_rabbitMqConnection?.IsOpen == true)
            {
                ConnectToRabbitMq();
            }
        }

        // Publish to RabbitMQ
        var body = System.Text.Encoding.UTF8.GetBytes(record.Payload);
        var properties = _channel!.CreateBasicProperties();
        properties.Persistent = true;
        properties.CorrelationId = record.CorrelationId;
        properties.DeliveryMode = 2; // persistent

        try
        {
            _channel.BasicPublish(
                exchange: "adnd.events",
                routingKey: $"game.{record.GameId}",
                mandatory: false,
                basicProperties: properties,
                body: body);

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
            evt = JsonSerializer.Deserialize(record.Payload, handlers[0].eventType) as IGameEvent;
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

    /// <summary>
    /// Get or create a RabbitMQ channel for a specific game queue.
    /// Used by RabbitMqEventBus for publishing.
    /// </summary>
    public IModel GetOrCreateGameChannel(Guid gameId)
    {
        return _channelsByGame.GetOrAdd(gameId.ToString(), _ =>
        {
            var host = _configuration["RabbitMq:Host"] ?? "localhost";
            var port = _configuration.GetValue<int>("RabbitMq:Port", 5672);
            var username = _configuration["RabbitMq:Username"] ?? "adnd";
            var password = _configuration["RabbitMq:Password"] ?? "adnd";
            var virtualHost = _configuration["RabbitMq:VirtualHost"] ?? "/adnd";

            var factory = new ConnectionFactory
            {
                HostName = host,
                Port = port,
                UserName = username,
                Password = password,
                VirtualHost = virtualHost
            };

            var connection = factory.CreateConnection();
            var channel = connection.CreateModel();

            // Declare game queue
            var queueName = $"game.{gameId}";
            channel.QueueDeclare(queueName, durable: true, exclusive: false, autoDelete: false, null);

            // Declare DLQ
            var dlqName = $"dlq.game.{gameId}";
            channel.QueueDeclare(dlqName, durable: true, exclusive: false, autoDelete: false, null);

            // Bind DLQ to exchange
            channel.QueueBind(dlqName, "adnd.events", $"dlq.game.{gameId}");

            // Bind game queue to exchange
            channel.QueueBind(queueName, "adnd.events", $"game.{gameId}");

            // Set dead-letter exchange on main queue
            var args = new Dictionary<string, object>
            {
                ["x-dead-letter-exchange"] = "adnd.events",
                ["x-dead-letter-routing-key"] = $"dlq.game.{gameId}"
            };
            channel.QueueDeclare(queueName, durable: true, exclusive: false, autoDelete: false, args);

            _logger.LogInformation("[RABBITMQ] DeclaredQueue | Queue={Queue} | DLQ={DLQ}", queueName, dlqName);

            return channel;
        });
    }

    public override async Task StopAsync(CancellationToken ct)
    {
        _channel?.Dispose();
        _rabbitMqConnection?.Close();
        await base.StopAsync(ct);
    }
}
