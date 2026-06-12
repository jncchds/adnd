using Adnd.Server.Data;
using Adnd.Server.Events;
using Adnd.Server.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Framing;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Adnd.Server.Services;

/// <summary>
/// Background service that polls EventRecords for pending/failed events
/// and publishes them to RabbitMQ. Also replays pending events on startup.
/// </summary>
public class EventBusWorker : BackgroundService
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

    public override async Task StartAsync(CancellationToken ct)
    {
        await base.StartAsync(ct);

        // Register handlers via reflection
        RegisterHandlers();

        // Connect to RabbitMQ
        ConnectToRabbitMq();

        // Replay pending events from previous run
        await ReplayPendingEvents(ct);

        _logger.LogInformation("EventBusWorker started with {HandlerCount} registered handlers", _handlerMap.Count);
    }

    private void RegisterHandlers()
    {
        var assembly = typeof(IGameEvent).Assembly;

        foreach (var type in assembly.GetTypes())
        {
            // Skip abstract classes and interfaces
            if (type.IsAbstract || type.IsInterface) continue;

            var interfaces = type.GetInterfaces()
                .Where(i => i.IsGenericType &&
                           i.GetGenericTypeDefinition() == typeof(IEventHandler<>));

            foreach (var iface in interfaces)
            {
                var eventType = iface.GetGenericArguments()[0];
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
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var pending = await context.EventRecords
                    .Where(e => e.Status == EventStatus.Pending || e.Status == EventStatus.Failed)
                    .OrderBy(e => e.CreatedAt)
                    .Take(100)
                    .ToListAsync(stoppingToken);

                foreach (var record in pending)
                {
                    await DispatchEvent(record, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in EventBusWorker poll loop");
            }

            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }

    /// <summary>
    /// Dispatch a single event record to RabbitMQ and then to handlers.
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
        if (!_handlerMap.TryGetValue(record.EventType, out var handlers))
        {
            _logger.LogWarning("[EVENT] NoHandlerForType | EventType={EventType} | EventId={EventId}",
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
        foreach (var (handlerId, _, handlerType) in handlers)
        {
            try
            {
                // Create a scoped service provider for the handler
                using var scope = _serviceProvider.CreateScope();
                var handlerInstance = scope.ServiceProvider.GetRequiredService(handlerType);

                // Get the HandleAsync method
                var handleMethod = handlerType.GetMethod("HandleAsync");
                if (handleMethod == null)
                {
                    _logger.LogError("[EVENT] NoHandleAsync | Handler={HandlerType} | EventId={EventId}",
                        handlerType.Name, record.Id);
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
