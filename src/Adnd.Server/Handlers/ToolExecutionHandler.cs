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
    private readonly RabbitMqEventBus _rabbitMq;

    public ToolExecutionHandler(IServiceProvider serviceProvider, ILogger<ToolExecutionHandler> logger, RabbitMqEventBus rabbitMq)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _rabbitMq = rabbitMq;
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
            var waitingPayload = JsonSerializer.Serialize(waitingEvent);
            var waitingHeaders = new Dictionary<string, object> { ["x-event-type"] = waitingEvent.GetType().FullName! };
            _rabbitMq.PublishToAgent(evt.GameId, waitingPayload, Guid.NewGuid().ToString(), waitingHeaders);
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
        var payload = JsonSerializer.Serialize(nextEvent);
        var headers = new Dictionary<string, object> { ["x-event-type"] = nextEvent.GetType().FullName! };
        _rabbitMq.PublishToAgent(evt.GameId, payload, Guid.NewGuid().ToString(), headers);

        _logger.LogInformation("[TOOL] Executed | SagaId={SagaId} | Index={Index} | Success={Success}",
            evt.SagaId, evt.ToolIndex, result.Success);
    }
}
