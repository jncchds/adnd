namespace Adnd.Server.Events;

public interface IGameEvent
{
    Guid GameId { get; }
}
