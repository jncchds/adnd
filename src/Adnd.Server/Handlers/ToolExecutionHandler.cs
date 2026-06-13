using Adnd.Server.Data;
using Adnd.Server.Events;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Adnd.Server.Handlers;

public class ToolExecutionHandler : IEventHandler<ToolCallRequested>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ToolExecutionHandler> _logger;

    public ToolExecutionHandler(IServiceProvider serviceProvider, ILogger<ToolExecutionHandler> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task HandleAsync(ToolCallRequested evt, CancellationToken ct)
    {
        _logger.LogInformation("[TOOL] Executing | SagaId={SagaId} | Index={Index} | Tool={Tool}",
            evt.SagaId, evt.ToolIndex, evt.ToolName);

        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var call = await context.AgentCalls.FirstOrDefaultAsync(c => c.Id == evt.SagaId, ct);
        if (call == null)
        {
            _logger.LogWarning("[TOOL] CallNotFound | SagaId={SagaId}", evt.SagaId);
            return;
        }

        // Execute the tool
        var toolRegistry = scope.ServiceProvider.GetRequiredService<IGMToolRegistry>();
        var result = await toolRegistry.ExecuteToolAsync(call.GameId, call.SessionId ?? Guid.Empty, evt.ToolName, evt.ToolArgs);

        var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();

        if (result.RequiresUserInput)
        {
            // Tool needs confirmation — save state and emit waiting event
            call.CurrentStep = SagaStep.ToolCallRequested;
            await context.SaveChangesAsync(ct);

            // Update game status for UI
            if (call.Game != null)
            {
                call.Game.LastGMAction = $"ToolCall: {evt.ToolName} (waiting confirmation)";
                call.Game.LastGMActionAt = DateTime.UtcNow;
                await context.SaveChangesAsync(ct);
            }

            await eventBus.PublishAsync(new AgentCallFailed(evt.SagaId, evt.GameId, $"Waiting user input for {evt.ToolName}"), ct);
            return;
        }

        // Update coordinator if one exists
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

        // Emit ToolCallCompleted — CoordinatorHandler will listen and emit next tool or follow-up
        await eventBus.PublishAsync(new ToolCallCompleted(
            evt.SagaId, evt.GameId, evt.ToolIndex, result.Output ?? "", result.Error), ct);

        _logger.LogInformation("[TOOL] Executed | SagaId={SagaId} | Index={Index} | Success={Success}",
            evt.SagaId, evt.ToolIndex, result.Success);
    }
}
