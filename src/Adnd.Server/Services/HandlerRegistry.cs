using System.Collections.Concurrent;
using Adnd.Server.Events;
using Microsoft.Extensions.DependencyInjection;

namespace Adnd.Server.Services;

public interface IHandlerRegistry
{
    IReadOnlyList<IEventHandler<T>> GetHandlers<T>() where T : IGameEvent;
}

public class HandlerRegistry(IServiceProvider sp) : IHandlerRegistry
{
    private readonly ConcurrentDictionary<Type, object> _cache = new();

    public IReadOnlyList<IEventHandler<T>> GetHandlers<T>() where T : IGameEvent
        => (IReadOnlyList<IEventHandler<T>>)_cache.GetOrAdd(typeof(T), _ =>
            sp.GetServices<IEventHandler<T>>().ToList());
}
