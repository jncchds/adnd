using MassTransit;
using Adnd.Server.Events;
using Adnd.Server.Handlers;

namespace Adnd.Server.Consumers;

/// <summary>
/// MassTransit consumer for player disconnect/reconnect events.
/// Thin adapter that delegates to PlayerDisconnectHandler.
/// </summary>
public class PlayerDisconnectConsumer :
    IConsumer<PlayerDisconnected>,
    IConsumer<PlayerReconnected>
{
    private readonly IServiceProvider _serviceProvider;

    public PlayerDisconnectConsumer(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public Task Consume(ConsumeContext<PlayerDisconnected> context)
    {
        using var scope = _serviceProvider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<PlayerDisconnectHandler>();
        return handler.HandleAsync(context.Message, context.CancellationToken);
    }

    public Task Consume(ConsumeContext<PlayerReconnected> context)
    {
        using var scope = _serviceProvider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<PlayerDisconnectHandler>();
        return handler.HandleAsync(context.Message, context.CancellationToken);
    }
}
