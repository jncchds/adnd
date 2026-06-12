namespace Adnd.Server.Services;

public interface IEventBus
{
    /// <summary>
    /// Publish an event to the specified game's queue.
    /// Event is persisted to EventRecord table and dispatched via RabbitMQ.
    /// </summary>
    Task PublishAsync<TEvent>(TEvent evt, CancellationToken ct = default) where TEvent : IGameEvent;

    /// <summary>
    /// Subscribe a handler to a specific event type.
    /// Used for dynamic subscription (e.g., per-game handlers).
    /// </summary>
    void Subscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : IGameEvent;

    /// <summary>
    /// Unsubscribe a handler from a specific event type.
    /// </summary>
    void Unsubscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : IGameEvent;
}
