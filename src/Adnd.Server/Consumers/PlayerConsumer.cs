using MassTransit;
using Adnd.Server.Events;
using Adnd.Server.Handlers;

namespace Adnd.Server.Consumers;

/// <summary>
/// MassTransit consumer for player join/leave events.
/// Thin adapter that delegates to PlayerHandler.
/// </summary>
public class PlayerConsumer :
    IConsumer<PlayerJoined>,
    IConsumer<PlayerLeft>
{
    private readonly IServiceProvider _serviceProvider;

    public PlayerConsumer(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public Task Consume(ConsumeContext<PlayerJoined> context)
    {
        using var scope = _serviceProvider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<PlayerHandler>();
        return handler.HandleAsync(context.Message, context.CancellationToken);
    }

    public Task Consume(ConsumeContext<PlayerLeft> context)
    {
        using var scope = _serviceProvider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<PlayerHandler>();
        return handler.HandleAsync(context.Message, context.CancellationToken);
    }
}
