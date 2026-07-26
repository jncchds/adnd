namespace Adnd.Server.Events;

public interface IEventHandler<T> where T : IGameEvent
{
    Task HandleAsync(T @event, CancellationToken ct);
}
