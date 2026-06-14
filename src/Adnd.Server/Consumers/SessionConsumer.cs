using MassTransit;
using Adnd.Server.Events;
using Adnd.Server.Handlers;

namespace Adnd.Server.Consumers;

/// <summary>
/// MassTransit consumer for session events.
/// Thin adapter that delegates to SessionHandler.
/// </summary>
public class SessionConsumer :
    IConsumer<SessionCreated>,
    IConsumer<SessionClosed>
{
    private readonly IServiceProvider _serviceProvider;

    public SessionConsumer(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public Task Consume(ConsumeContext<SessionCreated> context)
    {
        using var scope = _serviceProvider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<SessionHandler>();
        return handler.HandleAsync(context.Message, context.CancellationToken);
    }

    public Task Consume(ConsumeContext<SessionClosed> context)
    {
        using var scope = _serviceProvider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<SessionHandler>();
        return handler.HandleAsync(context.Message, context.CancellationToken);
    }
}
