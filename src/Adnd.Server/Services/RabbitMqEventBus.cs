using Adnd.Server.Data;
using Adnd.Server.Events;
using Adnd.Server.Models;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Framing;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Adnd.Server.Services;

/// <summary>
/// RabbitMQ-backed implementation of IEventBus.
/// Publishes events to per-game queues with durable persistence.
/// </summary>
public class RabbitMqEventBus : IEventBus
{
    private readonly AppDbContext _context;
    private readonly ILogger<RabbitMqEventBus> _logger;
    private readonly IConfiguration _configuration;
    private readonly ConcurrentDictionary<string, IModel> _channelsByGame = new();
    private readonly ConcurrentDictionary<string, IModel> _channelsByAgent = new();
    private readonly object _lock = new();

    public RabbitMqEventBus(
        AppDbContext context,
        ILogger<RabbitMqEventBus> logger,
        IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task PublishAsync<TEvent>(TEvent evt, CancellationToken ct = default) where TEvent : IGameEvent
    {
        // Create event record
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

        _context.EventRecords.Add(record);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("[EVENT] Queued | GameId={GameId} | EventType={EventType} | EventId={EventId}",
            evt.GameId, typeof(TEvent).Name, record.Id);
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

    /// <summary>
    /// Get or create a RabbitMQ channel for a specific game queue.
    /// Declares the queue, DLQ, and bindings if they don't exist.
    /// </summary>
    public IModel GetOrCreateGameChannel(Guid gameId)
    {
        return _channelsByGame.GetOrAdd(gameId.ToString(), _ =>
        {
            var channel = CreateChannel();

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

    /// <summary>
    /// Get or create a RabbitMQ channel for a specific agent queue.
    /// Used by GameAgent for reactive wakeup.
    /// </summary>
    public IModel GetOrCreateAgentChannel(Guid gameId)
    {
        return _channelsByAgent.GetOrAdd(gameId.ToString(), _ =>
        {
            var channel = CreateChannel();

            var queueName = $"agent.{gameId}";
            channel.QueueDeclare(queueName, durable: true, exclusive: false, autoDelete: false, null);
            channel.QueueBind(queueName, "adnd.events", $"agent.{gameId}");

            _logger.LogInformation("[RABBITMQ] DeclaredAgentQueue | Queue={Queue}", queueName);

            return channel;
        });
    }

    /// <summary>
    /// Publish an event to a specific game's queue.
    /// Called by EventBusWorker and AgentBus.
    /// </summary>
    public void PublishToGame(Guid gameId, string eventType, string payload, string? correlationId = null)
    {
        var channel = GetOrCreateGameChannel(gameId);

        var body = System.Text.Encoding.UTF8.GetBytes(payload);
        var properties = channel.CreateBasicProperties();
        properties.Persistent = true;
        properties.CorrelationId = correlationId;
        properties.DeliveryMode = 2; // persistent

        channel.BasicPublish(
            exchange: "adnd.events",
            routingKey: $"game.{gameId}",
            mandatory: false,
            basicProperties: properties,
            body: body);
    }

    /// <summary>
    /// Publish an event to a specific agent's queue with optional headers.
    /// The routing key is always 'agent.{gameId}' — the queue binding matches this.
    /// </summary>
    public void PublishToAgent(Guid gameId, string payload, string? correlationId = null, Dictionary<string, object>? headers = null)
    {
        var channel = GetOrCreateAgentChannel(gameId);

        var body = System.Text.Encoding.UTF8.GetBytes(payload);
        var props = channel.CreateBasicProperties();
        props.Persistent = true;
        props.CorrelationId = correlationId;
        props.DeliveryMode = 2; // persistent
        if (headers != null && headers.Count > 0)
        {
            props.Headers = headers;
        }

        channel.BasicPublish(
            exchange: "adnd.events",
            routingKey: $"agent.{gameId}",
            mandatory: false,
            basicProperties: props,
            body: body);
    }

    private IModel CreateChannel()
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

        // Declare exchange if not exists
        channel.ExchangeDeclare("adnd.events", ExchangeType.Direct, durable: true);

        return channel;
    }
}
