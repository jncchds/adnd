using Adnd.Server.Events;
using Adnd.Server.Services;
using Microsoft.Extensions.Logging;

namespace Adnd.Server.Handlers;

public class AgentCallCompletedHandler : IEventHandler<AgentCallCompleted>
{
    private readonly ILogger<AgentCallCompletedHandler> _logger;

    public AgentCallCompletedHandler(ILogger<AgentCallCompletedHandler> logger)
    {
        _logger = logger;
    }

    public Task HandleAsync(AgentCallCompleted evt, CancellationToken ct)
    {
        _logger.LogInformation("[SAGA] Completed | SagaId={SagaId}", evt.SagaId);
        return Task.CompletedTask;
    }
}
