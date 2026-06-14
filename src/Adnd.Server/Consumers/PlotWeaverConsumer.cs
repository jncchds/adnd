using MassTransit;
using Adnd.Server.Events;
using Adnd.Server.Handlers;

namespace Adnd.Server.Consumers;

/// <summary>
/// MassTransit consumer for plot weaving events.
/// Thin adapter that delegates to PlotWeaverHandler.
/// </summary>
public class PlotWeaverConsumer :
    IConsumer<GameStarted>,
    IConsumer<CombatEnded>,
    IConsumer<CombatStarted>,
    IConsumer<NPCCreated>,
    IConsumer<NPCDeleted>,
    IConsumer<NPCUpdated>,
    IConsumer<CharacterUpdated>,
    IConsumer<StorySwayed>,
    IConsumer<PlayerJoined>,
    IConsumer<MessageSent>,
    IConsumer<PlotThreadCreated>,
    IConsumer<PlotThreadUpdated>
{
    private readonly IServiceProvider _serviceProvider;

    public PlotWeaverConsumer(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public Task Consume(ConsumeContext<GameStarted> context)
    {
        using var scope = _serviceProvider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<PlotWeaverHandler>();
        return handler.HandleAsync(context.Message, context.CancellationToken);
    }

    public Task Consume(ConsumeContext<CombatEnded> context)
    {
        using var scope = _serviceProvider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<PlotWeaverHandler>();
        return handler.HandleAsync(context.Message, context.CancellationToken);
    }

    public Task Consume(ConsumeContext<CombatStarted> context)
    {
        using var scope = _serviceProvider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<PlotWeaverHandler>();
        return handler.HandleAsync(context.Message, context.CancellationToken);
    }

    public Task Consume(ConsumeContext<NPCCreated> context)
    {
        using var scope = _serviceProvider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<PlotWeaverHandler>();
        return handler.HandleAsync(context.Message, context.CancellationToken);
    }

    public Task Consume(ConsumeContext<NPCDeleted> context)
    {
        using var scope = _serviceProvider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<PlotWeaverHandler>();
        return handler.HandleAsync(context.Message, context.CancellationToken);
    }

    public Task Consume(ConsumeContext<NPCUpdated> context)
    {
        using var scope = _serviceProvider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<PlotWeaverHandler>();
        return handler.HandleAsync(context.Message, context.CancellationToken);
    }

    public Task Consume(ConsumeContext<CharacterUpdated> context)
    {
        using var scope = _serviceProvider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<PlotWeaverHandler>();
        return handler.HandleAsync(context.Message, context.CancellationToken);
    }

    public Task Consume(ConsumeContext<StorySwayed> context)
    {
        using var scope = _serviceProvider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<PlotWeaverHandler>();
        return handler.HandleAsync(context.Message, context.CancellationToken);
    }

    public Task Consume(ConsumeContext<PlayerJoined> context)
    {
        using var scope = _serviceProvider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<PlotWeaverHandler>();
        return handler.HandleAsync(context.Message, context.CancellationToken);
    }

    public Task Consume(ConsumeContext<MessageSent> context)
    {
        using var scope = _serviceProvider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<PlotWeaverHandler>();
        return handler.HandleAsync(context.Message, context.CancellationToken);
    }

    public Task Consume(ConsumeContext<PlotThreadCreated> context)
    {
        using var scope = _serviceProvider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<PlotWeaverHandler>();
        return handler.HandleAsync(context.Message, context.CancellationToken);
    }

    public Task Consume(ConsumeContext<PlotThreadUpdated> context)
    {
        using var scope = _serviceProvider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<PlotWeaverHandler>();
        return handler.HandleAsync(context.Message, context.CancellationToken);
    }
}
