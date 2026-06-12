namespace Adnd.Server.Events;

public interface IEventHandler<TEvent> where TEvent : IGameEvent
{
    Task HandleAsync(TEvent evt, CancellationToken ct = default);
}
