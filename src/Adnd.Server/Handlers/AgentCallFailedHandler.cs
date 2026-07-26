using Adnd.Server.Data;
using Adnd.Server.Events;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Adnd.Server.Handlers;

public class AgentCallFailedHandler : IEventHandler<AgentCallFailed>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AgentCallFailedHandler> _logger;

    public AgentCallFailedHandler(IServiceScopeFactory scopeFactory, ILogger<AgentCallFailedHandler> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task HandleAsync(AgentCallFailed evt, CancellationToken ct)
    {
        _logger.LogError("[SAGA] Failed | SagaId={SagaId} | Error={Error}", evt.SagaId, evt.Error);

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var call = await context.AgentCalls.FirstOrDefaultAsync(c => c.Id == evt.SagaId, ct);
        if (call == null) return;

        call.Status = AgentCallStatus.Failed;
        call.Error = evt.Error;
        call.CompletedAt = DateTime.UtcNow;
        call.CurrentStep = SagaStep.Failed;

        await context.SaveChangesAsync(ct);

        // Abandon any coordinators for this saga
        var coordinators = context.ToolCallCoordinators.Where(c => c.SagaId == evt.SagaId && c.Status == CoordinatorStatus.Active);
        foreach (var coord in coordinators)
        {
            coord.Status = CoordinatorStatus.Abandoned;
            coord.CompletedAt = DateTime.UtcNow;
        }
        await context.SaveChangesAsync(ct);
    }
}
