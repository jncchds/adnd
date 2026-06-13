using Adnd.Server.Handlers;
using Adnd.Server.Events;
using System.Reflection;

namespace Adnd.Server.Services;

public interface IHandlerRegistry
{
    IReadOnlyDictionary<string, List<Type>> Handlers { get; }
}

public class HandlerRegistry : IHandlerRegistry
{
    private readonly Dictionary<string, List<Type>> _handlers;

    public IReadOnlyDictionary<string, List<Type>> Handlers => _handlers;

    public HandlerRegistry()
    {
        _handlers = new();
        RegisterHandlers();
    }

    private void RegisterHandlers()
    {
        var assembly = typeof(GameLifecycleHandler).Assembly;
        var iEventHandlerType = typeof(IEventHandler<>);

        foreach (var type in assembly.GetTypes()
            .Where(t => t.Namespace == "Adnd.Server.Handlers"
                     && !t.IsAbstract && !t.IsInterface
                     && t.GetInterfaces().Any(i =>
                         i.IsGenericType &&
                         i.GetGenericTypeDefinition() == iEventHandlerType)))
        {
            var handlerInterfaces = type.GetInterfaces()
                .Where(i => i.IsGenericType &&
                           i.GetGenericTypeDefinition() == iEventHandlerType);

            foreach (var handlerInterface in handlerInterfaces)
            {
                var eventType = handlerInterface.GetGenericArguments()[0];
                var key = eventType.FullName!;

                if (!_handlers.TryGetValue(key, out var list))
                {
                    list = new List<Type>();
                    _handlers[key] = list;
                }
                list.Add(type);
            }
        }
    }
}
