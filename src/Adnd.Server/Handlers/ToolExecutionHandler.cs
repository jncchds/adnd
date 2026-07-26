using Adnd.Server.Data;
using Adnd.Server.Events;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Wolverine;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Adnd.Server.Handlers;

public class ToolExecutionHandler : IEventHandler<ToolCallRequested>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ToolExecutionHandler> _logger;
    private readonly IEventBus _eventBus;

    public ToolExecutionHandler(IServiceScopeFactory scopeFactory, ILogger<ToolExecutionHandler> logger, IEventBus eventBus)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _eventBus = eventBus;
    }

    public async Task HandleAsync(ToolCallRequested evt, CancellationToken ct)
    {
        _logger.LogInformation("[TOOL] Executing | SagaId={SagaId} | Index={Index} | Tool={Tool}",
            evt.SagaId, evt.ToolIndex, evt.ToolName);

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var call = await context.AgentCalls.FirstOrDefaultAsync(c => c.Id == evt.SagaId, ct);
        if (call == null)
        {
            _logger.LogWarning("[TOOL] CallNotFound | SagaId={SagaId}", evt.SagaId);
            return;
        }

        var toolRegistry = scope.ServiceProvider.GetRequiredService<IGMToolRegistry>();
        var result = await toolRegistry.ExecuteToolAsync(call.GameId, call.SessionId ?? Guid.Empty, evt.ToolName, evt.ToolArgs);

        if (result.RequiresUserInput)
        {
            call.CurrentStep = SagaStep.ToolCallRequested;
            await context.SaveChangesAsync(ct);

            // Update game status via context (not nav prop) to avoid EF tracking issues
            var game = await context.Games.FindAsync(call.GameId);
            if (game != null)
            {
                game.LastGMAction = $"ToolCall: {evt.ToolName} (waiting confirmation)";
                game.LastGMActionAt = DateTime.UtcNow;
                await context.SaveChangesAsync(ct);
            }

            var waitingEvent = new ToolCallWaitingConfirmation(
                evt.SagaId,
                evt.GameId,
                evt.ToolName,
                call.Id.ToString());
            await _eventBus.PublishAsync(waitingEvent, ct);
            return;
        }

        var coordinator = await context.ToolCallCoordinators
            .FirstOrDefaultAsync(c => c.SagaId == evt.SagaId && c.Status == CoordinatorStatus.Active, ct);

        if (coordinator != null)
        {
            var toolResult = new ToolCallResult
            {
                Index = evt.ToolIndex,
                ToolName = evt.ToolName,
                Result = result.Output ?? "",
                Error = result.Error
            };
            coordinator.CompletedTools.Add(toolResult);
            coordinator.CurrentIndex = evt.ToolIndex + 1;

            if (evt.ToolIndex + 1 >= coordinator.TotalTools)
            {
                coordinator.Status = CoordinatorStatus.Completed;
                coordinator.CompletedAt = DateTime.UtcNow;
            }

            await context.SaveChangesAsync(ct);
        }

        var nextEvent = new ToolCallCompleted(evt.SagaId, evt.GameId, evt.ToolIndex, result.Output ?? "", result.Error);
        await _eventBus.PublishAsync(nextEvent, ct);

        _logger.LogInformation("[TOOL] Executed | SagaId={SagaId} | Index={Index} | Success={Success}",
            evt.SagaId, evt.ToolIndex, result.Success);
    }
}
