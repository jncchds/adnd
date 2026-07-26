using Adnd.Server.Data;
using Adnd.Server.Events;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Wolverine;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Adnd.Server.Handlers;

public class CoordinatorHandler : IEventHandler<ToolCallCompleted>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CoordinatorHandler> _logger;
    private readonly IEventBus _eventBus;

    public CoordinatorHandler(IServiceScopeFactory scopeFactory, ILogger<CoordinatorHandler> logger, IEventBus eventBus)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _eventBus = eventBus;
    }

    public async Task HandleAsync(ToolCallCompleted evt, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var coordinator = await context.ToolCallCoordinators
            .FirstOrDefaultAsync(c => c.SagaId == evt.SagaId && c.Status == CoordinatorStatus.Active, ct);

        if (coordinator == null) return;

        if (coordinator.CurrentIndex >= coordinator.TotalTools)
        {
            coordinator.Status = CoordinatorStatus.Completed;
            coordinator.CompletedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(ct);

            var toolResults = coordinator.CompletedTools
                .OrderBy(t => t.Index)
                .Select(t => new ToolResult(t.Index, t.ToolName, t.Result, t.Error))
                .ToList();

            var followUpEvent = new LLMFollowUpRequested(evt.SagaId, evt.GameId, toolResults);
            await _eventBus.PublishAsync(followUpEvent, ct);

            _logger.LogInformation("[COORD] AllToolsDone | SagaId={SagaId} | Tools={Count}", evt.SagaId, toolResults.Count);
            return;
        }

        var tools = JsonSerializer.Deserialize<List<ToolCallInfo>>(coordinator.ToolsJson) ?? new();
        if (coordinator.CurrentIndex >= tools.Count)
        {
            _logger.LogWarning("[COORD] NoNextTool | SagaId={SagaId} | Index={Index} | Available={Count}",
                evt.SagaId, coordinator.CurrentIndex, tools.Count);
            // No more tools — mark completed and emit follow-up with empty results
            coordinator.Status = CoordinatorStatus.Completed;
            coordinator.CompletedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(ct);

            var toolResults = coordinator.CompletedTools
                .OrderBy(t => t.Index)
                .Select(t => new ToolResult(t.Index, t.ToolName, t.Result, t.Error))
                .ToList();

            var followUpEvent = new LLMFollowUpRequested(evt.SagaId, evt.GameId, toolResults);
            await _eventBus.PublishAsync(followUpEvent, ct);
            return;
        }
        var nextTool = tools[coordinator.CurrentIndex];

        var nextEvent = new ToolCallRequested(evt.SagaId, evt.GameId, coordinator.CurrentIndex, nextTool.Name, nextTool.Arguments);
        await _eventBus.PublishAsync(nextEvent, ct);

        _logger.LogInformation("[COORD] NextTool | SagaId={SagaId} | Index={Index}/{Total}",
            evt.SagaId, coordinator.CurrentIndex, coordinator.TotalTools);
    }
}
