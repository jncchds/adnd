using Wolverine;

namespace Adnd.Server.Services;

public interface IEventBus
{
    Task PublishAsync<T>(T @event, CancellationToken ct = default) where T : class;
}

public class EventBusWorker(IMessageBus wolverine) : IEventBus
{
    public Task PublishAsync<T>(T @event, CancellationToken ct = default) where T : class
        => wolverine.PublishAsync(@event).AsTask();
}
