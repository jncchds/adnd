using Adnd.Server.Data;
using Adnd.Server.Events;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Adnd.Server.Handlers;

public class ToolCallWaitingConfirmationHandler :
    IEventHandler<ToolCallWaitingConfirmation>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ToolCallWaitingConfirmationHandler> _logger;

    public ToolCallWaitingConfirmationHandler(
        IServiceScopeFactory scopeFactory,
        ILogger<ToolCallWaitingConfirmationHandler> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task HandleAsync(ToolCallWaitingConfirmation evt, CancellationToken ct = default)
    {
        _logger.LogInformation("[TOOL] WaitingConfirmation | SagaId={SagaId} | Tool={Tool} | CallId={CallId}",
            evt.SagaId, evt.ToolName, evt.ToolCallId);

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var call = await context.AgentCalls.FirstOrDefaultAsync(c => c.Id == evt.SagaId, ct);
        if (call == null) return;

        call.CurrentStep = SagaStep.ToolCallRequested;
        // Do NOT mark as Failed — keep coordinator intact
        await context.SaveChangesAsync(ct);
    }
}
