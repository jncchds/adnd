using Adnd.Server.Data;
using Adnd.Server.Events;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Adnd.Server.Handlers;

public class SagaOrchestratorHandler : IEventHandler<AgentCallQueued>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SagaOrchestratorHandler> _logger;
    private readonly RabbitMqEventBus _rabbitMq;

    public SagaOrchestratorHandler(
        IServiceProvider serviceProvider,
        ILogger<SagaOrchestratorHandler> logger,
        RabbitMqEventBus rabbitMq)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _rabbitMq = rabbitMq;
    }

    public async Task HandleAsync(AgentCallQueued evt, CancellationToken ct)
    {
        _logger.LogInformation("[SAGA] Orchestrating | SagaId={SagaId}", evt.SagaId);

        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var call = await context.AgentCalls
            .FirstOrDefaultAsync(c => c.Id == evt.SagaId && c.GameId == evt.GameId, ct);

        if (call == null)
        {
            _logger.LogWarning("[SAGA] CallNotFound | SagaId={SagaId}", evt.SagaId);
            return;
        }

        call.CurrentStep = SagaStep.Init;
        await context.SaveChangesAsync(ct);

        IGameEvent nextEvent = call.Action switch
        {
            AgentAction.Narrate or AgentAction.Generate or AgentAction.OpenNarrative
                => new LLMDispatchRequested(evt.SagaId, evt.GameId, "You are the Game Master for a TTRPG session.", call.Input ?? "Continue the narrative.", null),
            AgentAction.Nudge => new LLMDispatchRequested(evt.SagaId, evt.GameId, "You are the Game Master for a TTRPG session. The creator has sent a narrative nudge.", call.Input ?? "Incorporate the direction.", null),
            AgentAction.Query => new LLMDispatchRequested(evt.SagaId, evt.GameId, "You are a helpful TTRPG assistant.", call.Input ?? "Answer the question.", null),
            AgentAction.Suggest => new LLMDispatchRequested(evt.SagaId, evt.GameId, "You are a creative TTRPG Game Master assistant.", call.Input ?? "Suggest plot continuations.", null),
            AgentAction.ManageState => new AgentCallCompleted(evt.SagaId, evt.GameId),
            _ => new AgentCallFailed(evt.SagaId, evt.GameId, $"Unknown action: {call.Action}")
        };

        var payload = JsonSerializer.Serialize(nextEvent);
        var headers = new Dictionary<string, object> { ["x-event-type"] = nextEvent.GetType().FullName };
        _rabbitMq.PublishToAgent(evt.GameId, "adnd.saga", payload, Guid.NewGuid().ToString(), headers);

        call.CurrentStep = call.Action == AgentAction.ManageState ? SagaStep.Completed : SagaStep.LLMDispatchRequested;
        await context.SaveChangesAsync(ct);

        _logger.LogInformation("[SAGA] NextStep | SagaId={SagaId} | Step={Step}", evt.SagaId, call.CurrentStep);
    }
}
