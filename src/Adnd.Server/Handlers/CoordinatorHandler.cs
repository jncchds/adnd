using Adnd.Server.Data;
using Adnd.Server.Events;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Adnd.Server.Handlers;

public class CoordinatorHandler : IEventHandler<ToolCallCompleted>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CoordinatorHandler> _logger;

    public CoordinatorHandler(IServiceProvider serviceProvider, ILogger<CoordinatorHandler> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task HandleAsync(ToolCallCompleted evt, CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
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

            var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();
            await eventBus.PublishAsync(new LLMFollowUpRequested(evt.SagaId, evt.GameId, toolResults), ct);

            _logger.LogInformation("[COORD] AllToolsDone | SagaId={SagaId} | Tools={Count}", evt.SagaId, toolResults.Count);
            return;
        }

        // Parse tools from JSON and emit next tool call
        var tools = JsonSerializer.Deserialize<List<ToolCallInfo>>(coordinator.ToolsJson) ?? new();
        var nextTool = tools[coordinator.CurrentIndex];

        var eventBus2 = scope.ServiceProvider.GetRequiredService<IEventBus>();
        await eventBus2.PublishAsync(new ToolCallRequested(
            evt.SagaId, evt.GameId, coordinator.CurrentIndex, nextTool.Name, nextTool.Arguments), ct);

        _logger.LogInformation("[COORD] NextTool | SagaId={SagaId} | Index={Index}/{Total}",
            evt.SagaId, coordinator.CurrentIndex, coordinator.TotalTools);
    }
}
