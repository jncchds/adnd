using MassTransit;
using Adnd.Server.Events;
using Adnd.Server.Handlers;

namespace Adnd.Server.Consumers;

/// <summary>
/// MassTransit consumer for game lifecycle events.
/// Thin adapter that delegates to GameLifecycleHandler.
/// </summary>
public class GameLifecycleConsumer :
    IConsumer<GameCreated>,
    IConsumer<GameStarted>,
    IConsumer<GameArchived>,
    IConsumer<GamePaused>,
    IConsumer<GameResumed>,
    IConsumer<GameNarrationStarted>
{
    private readonly IServiceProvider _serviceProvider;

    public GameLifecycleConsumer(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public Task Consume(ConsumeContext<GameCreated> context)
    {
        using var scope = _serviceProvider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<GameLifecycleHandler>();
        return handler.HandleAsync(context.Message, context.CancellationToken);
    }

    public Task Consume(ConsumeContext<GameStarted> context)
    {
        using var scope = _serviceProvider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<GameLifecycleHandler>();
        return handler.HandleAsync(context.Message, context.CancellationToken);
    }

    public Task Consume(ConsumeContext<GameArchived> context)
    {
        using var scope = _serviceProvider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<GameLifecycleHandler>();
        return handler.HandleAsync(context.Message, context.CancellationToken);
    }

    public Task Consume(ConsumeContext<GamePaused> context)
    {
        using var scope = _serviceProvider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<GameLifecycleHandler>();
        return handler.HandleAsync(context.Message, context.CancellationToken);
    }

    public Task Consume(ConsumeContext<GameResumed> context)
    {
        using var scope = _serviceProvider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<GameLifecycleHandler>();
        return handler.HandleAsync(context.Message, context.CancellationToken);
    }

    public Task Consume(ConsumeContext<GameNarrationStarted> context)
    {
        using var scope = _serviceProvider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<GameLifecycleHandler>();
        return handler.HandleAsync(context.Message, context.CancellationToken);
    }
}
