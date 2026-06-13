using Adnd.Server.Data;
using Adnd.Server.Events;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Adnd.Server.Handlers;

public class LLMResponseHandler : IEventHandler<LLMResponseReceived>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<LLMResponseHandler> _logger;

    public LLMResponseHandler(IServiceProvider serviceProvider, ILogger<LLMResponseHandler> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task HandleAsync(LLMResponseReceived evt, CancellationToken ct)
    {
        _logger.LogInformation("[SAGA] LLMResponse | SagaId={SagaId} | HasTools={HasTools}", evt.SagaId, evt.HasToolCalls);

        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var call = await context.AgentCalls.FirstOrDefaultAsync(c => c.Id == evt.SagaId, ct);
        if (call == null)
        {
            _logger.LogWarning("[SAGA] CallNotFound | SagaId={SagaId}", evt.SagaId);
            return;
        }

        var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();

        if (!evt.HasToolCalls)
        {
            // No tools — emit narrative directly
            call.CurrentStep = SagaStep.NarrativeReady;
            await context.SaveChangesAsync(ct);

            await eventBus.PublishAsync(new NarrativeReady(evt.SagaId, evt.GameId, evt.Response), ct);
            return;
        }

        // Has tool calls — check if we already have a coordinator
        var coordinator = await context.ToolCallCoordinators
            .FirstOrDefaultAsync(c => c.SagaId == evt.SagaId && c.Status == CoordinatorStatus.Active, ct);

        if (coordinator == null)
        {
            // Create new coordinator with extracted tool calls
            var toolCalls = ExtractToolCalls(evt.Response);
            coordinator = new ToolCallCoordinator
            {
                SagaId = evt.SagaId,
                GameId = call.GameId,
                TotalTools = evt.ToolCallCount,
                CurrentIndex = 0,
                ToolsJson = JsonSerializer.Serialize(toolCalls),
                Status = CoordinatorStatus.Active
            };
            context.ToolCallCoordinators.Add(coordinator);
            await context.SaveChangesAsync(ct);

            _logger.LogInformation("[SAGA] CoordinatorCreated | SagaId={SagaId} | Total={Total}", evt.SagaId, evt.ToolCallCount);
        }

        // Emit first tool call
        var tools = JsonSerializer.Deserialize<List<ToolCallInfo>>(coordinator.ToolsJson) ?? new();
        var firstTool = tools.FirstOrDefault();

        call.CurrentStep = SagaStep.ToolCallRequested;
        await context.SaveChangesAsync(ct);

        await eventBus.PublishAsync(new ToolCallRequested(
            evt.SagaId, evt.GameId, 0, firstTool?.Name ?? "unknown", firstTool?.Arguments ?? "{}"), ct);
    }

    private List<ToolCallInfo> ExtractToolCalls(string response)
    {
        var tools = new List<ToolCallInfo>();
        try
        {
            var doc = JsonDocument.Parse(response);
            if (doc.RootElement.TryGetProperty("tool_calls", out var tcProp))
            {
                foreach (var tc in tcProp.EnumerateArray())
                {
                    tools.Add(new ToolCallInfo
                    {
                        Name = tc.GetProperty("name").GetString() ?? "",
                        Arguments = tc.GetProperty("arguments").ToString()
                    });
                }
            }
        }
        catch
        {
            // Fallback: regex-based extraction
            tools = ExtractToolCallsRegex(response);
        }
        return tools;
    }

    private List<ToolCallInfo> ExtractToolCallsRegex(string response)
    {
        var tools = new List<ToolCallInfo>();
        var namePattern = new System.Text.RegularExpressions.Regex(@"""name""\s*:\s*""([^""]+)""");
        var argsPattern = new System.Text.RegularExpressions.Regex(@"""arguments""\s*:\s*(\{[^}]+\})");

        var names = namePattern.Matches(response);
        var args = argsPattern.Matches(response);

        for (int i = 0; i < names.Count; i++)
        {
            if (i < args.Count)
            {
                tools.Add(new ToolCallInfo
                {
                    Name = names[i].Groups[1].Value,
                    Arguments = args[i].Groups[1].Value
                });
            }
        }
        return tools;
    }
}
