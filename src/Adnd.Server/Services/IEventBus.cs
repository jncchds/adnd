using Adnd.Server.Events;

namespace Adnd.Server.Services;

public interface IEventBus
{
    /// <summary>
    /// Publish an event to the specified game's queue.
    /// Event is persisted by Wolverine and dispatched via PostgreSQL.
    /// </summary>
    Task PublishAsync<TEvent>(TEvent evt, CancellationToken ct = default) where TEvent : IGameEvent;
}
