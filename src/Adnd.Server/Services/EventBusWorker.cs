using Adnd.Server.Events;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Adnd.Server.Services;

/// <summary>
/// Background service that implements IEventBus with Wolverine/PostgreSQL delivery.
/// Publishes events directly via IMessageContext — Wolverine handles durability,
/// retry, and dead-letter queue. No manual EventRecord persistence needed.
/// </summary>
public class EventBusWorker : BackgroundService, IEventBus
{
    private readonly ILogger<EventBusWorker> _logger;
    private readonly IMessageContext _messageContext;

    public EventBusWorker(
        ILogger<EventBusWorker> logger,
        IMessageContext messageContext)
    {
        _logger = logger;
        _messageContext = messageContext;
    }

    // ==================== IEventBus Implementation ====================

    public async Task PublishAsync<TEvent>(TEvent evt, CancellationToken ct = default) where TEvent : IGameEvent
    {
        _logger.LogInformation("[EVENT] Queued | GameId={GameId} | EventType={EventType} | EventId={EventId}",
            evt.GameId, typeof(TEvent).Name, Guid.NewGuid());

        await _messageContext.PublishAsync(evt);

        _logger.LogDebug("[EVENT] Published | GameId={GameId} | EventType={EventType}",
            evt.GameId, typeof(TEvent).Name);
    }

    // ==================== Admin helpers (for backward compatibility) ====================

    /// <summary>
    /// Stub — EventRecord persistence removed. Wolverine handles durability.
    /// </summary>
    public async Task<int> PushPendingEventsAsync(Guid gameId)
    {
        _logger.LogWarning("[EVENT] PushPendingEvents called but no pending events exist (EventRecord persistence removed)");
        return 0;
    }

    /// <summary>
    /// Stub — EventRecord persistence removed. Wolverine handles durability.
    /// </summary>
    public async Task<int> GetPendingEventsCountAsync(Guid gameId)
    {
        return 0;
    }

    public override async Task StartAsync(CancellationToken ct)
    {
        await base.StartAsync(ct);
        _logger.LogInformation("EventBusWorker started — Wolverine consumers handle event dispatch");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // No polling loop — event-driven via Wolverine consumer.
        // The service runs only to keep the process alive and handle shutdown.
        await Task.CompletedTask;
    }

    public override async Task StopAsync(CancellationToken ct)
    {
        // Wolverine handles connection lifecycle — no manual cleanup needed
        await base.StopAsync(ct);
    }
}
