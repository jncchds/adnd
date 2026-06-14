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
    private readonly RabbitMqEventBus _rabbitMq;
    private readonly IEventBus _eventBus;

    public LLMResponseHandler(IServiceProvider serviceProvider, ILogger<LLMResponseHandler> logger, RabbitMqEventBus rabbitMq, IEventBus eventBus)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _rabbitMq = rabbitMq;
        _eventBus = eventBus;
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

        if (!evt.HasToolCalls)
        {
            // Special handling for GenerateInitialThreads — parse JSON and create PlotThread entities
            if (call.Action == AgentAction.GenerateInitialThreads)
            {
                call.CurrentStep = SagaStep.NarrativeReady;
                call.Output = evt.Response;
                await context.SaveChangesAsync(ct);

                var threads = await ParseAndSaveInitialThreads(evt.SagaId, evt.GameId, evt.Response, context);

                _logger.LogInformation("[SAGA] InitialThreadsSaved | SagaId={SagaId} | ThreadCount={Count}", evt.SagaId, threads);
                return;
            }

            call.CurrentStep = SagaStep.NarrativeReady;
            await context.SaveChangesAsync(ct);

            var narrativeEvent = new NarrativeReady(evt.SagaId, evt.GameId, evt.Response);
            var narrativePayload = JsonSerializer.Serialize(narrativeEvent);
            var narrativeHeaders = new Dictionary<string, object> { ["x-event-type"] = narrativeEvent.GetType().FullName! };
            _rabbitMq.PublishToAgent(evt.GameId, narrativePayload, Guid.NewGuid().ToString(), narrativeHeaders);
            return;
        }

        var coordinator = await context.ToolCallCoordinators
            .FirstOrDefaultAsync(c => c.SagaId == evt.SagaId && c.Status == CoordinatorStatus.Active, ct);

        if (coordinator == null)
        {
            var toolCalls = ExtractToolCalls(evt.Response);
            coordinator = new ToolCallCoordinator
            {
                SagaId = evt.SagaId,
                GameId = call.GameId,
                TotalTools = toolCalls.Count,  // Use actual extracted count, not heuristic
                CurrentIndex = 0,
                ToolsJson = JsonSerializer.Serialize(toolCalls),
                Status = CoordinatorStatus.Active
            };
            context.ToolCallCoordinators.Add(coordinator);
            await context.SaveChangesAsync(ct);
            _logger.LogInformation("[SAGA] CoordinatorCreated | SagaId={SagaId} | Total={Total}", evt.SagaId, toolCalls.Count);
        }

        var tools = JsonSerializer.Deserialize<List<ToolCallInfo>>(coordinator.ToolsJson) ?? new();
        var firstTool = tools.FirstOrDefault();

        call.CurrentStep = SagaStep.ToolCallRequested;
        await context.SaveChangesAsync(ct);

        var nextEvent = new ToolCallRequested(evt.SagaId, evt.GameId, 0, firstTool?.Name ?? "unknown", firstTool?.Arguments ?? "{}");
        var payload = JsonSerializer.Serialize(nextEvent);
        var headers = new Dictionary<string, object> { ["x-event-type"] = nextEvent.GetType().FullName! };
        _rabbitMq.PublishToAgent(evt.GameId, payload, Guid.NewGuid().ToString(), headers);
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
            tools = ExtractToolCallsRegex(response);
        }
        return tools;
    }

    /// <summary>
    /// Parse LLM response for GenerateInitialThreads and save PlotThread entities.
    /// </summary>
    private async Task<int> ParseAndSaveInitialThreads(Guid sagaId, Guid gameId, string response, AppDbContext context)
    {
        try
        {
            var extractedJson = JsonExtract.Extract(response);
            if (extractedJson == null)
            {
                _logger.LogWarning("[SAGA] FailedToExtractJsonForInitialThreads | SagaId={SagaId}", sagaId);
                return 0;
            }

            var threads = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(extractedJson);
            if (threads == null || !threads.Any())
            {
                _logger.LogWarning("[SAGA] NoThreadsParsed | SagaId={SagaId}", sagaId);
                return 0;
            }

            var newThreads = new List<PlotThread>();
            foreach (var threadData in threads)
            {
                var thread = new PlotThread
                {
                    GameId = gameId,
                    Title = threadData.GetValueOrDefault("title")?.ToString() ?? "Untitled Thread",
                    Category = Enum.TryParse<PlotThreadCategory>(threadData.GetValueOrDefault("category")?.ToString(), true, out var cat)
                        ? cat : PlotThreadCategory.Personal,
                    Description = threadData.GetValueOrDefault("description")?.ToString() ?? "",
                    NextMilestone = threadData.GetValueOrDefault("nextMilestone")?.ToString(),
                    Foreshadowing = threadData.GetValueOrDefault("foreshadowing")?.ToString(),
                    Momentum = 0f,
                    RelevanceScore = 0.5f,
                    Status = PlotThreadStatus.Active
                };
                newThreads.Add(thread);
            }

            context.PlotThreads.AddRange(newThreads);
            await context.SaveChangesAsync();

            _logger.LogInformation("[SAGA] SavedInitialThreads | SagaId={SagaId} | Count={Count}", sagaId, newThreads.Count);
            return newThreads.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SAGA] FailedToParseInitialThreads | SagaId={SagaId}", sagaId);
            return 0;
        }
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
