using MassTransit;
using Adnd.Server.Events;
using Adnd.Server.Models;
using Adnd.Server.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Adnd.Server.Services;

/// <summary>
/// MassTransit saga state machine for the agent call lifecycle.
/// Tracks: Init → LLMDispatched → ToolExecution → LLMFollowUp → NarrativeReady → Complete
/// Persists state to PostgreSQL via SagaDbContext — survives container restarts.
/// </summary>
public class AgentSaga :
    SagaStateMachine<AgentSagaData>,
    IConsumer<AgentCallQueued>,
    IConsumer<LLMDispatchRequested>,
    IConsumer<LLMResponseReceived>,
    IConsumer<ToolCallRequested>,
    IConsumer<ToolCallCompleted>,
    IConsumer<LLMFollowUpRequested>,
    IConsumer<NarrativeReady>
{
    public readonly State<AgentSagaData> Orchestrate;
    public readonly State<AgentSagaData> ExecuteTools;
    public readonly State<AgentSagaData> FollowUpLLM;
    public readonly State<AgentSagaData> Complete;

    public readonly Event<AgentCallQueued> AgentCallQueued;
    public readonly Event<LLMDispatchRequested> LLMDispatchRequested;
    public readonly Event<LLMResponseReceived> LLMResponseReceived;
    public readonly Event<ToolCallRequested> ToolCallRequested;
    public readonly Event<ToolCallCompleted> ToolCallCompleted;
    public readonly Event<LLMFollowUpRequested> LLMFollowUpRequested;
    public readonly Event<NarrativeReady> NarrativeReady;

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AgentSaga> _logger;

    public AgentSaga(IServiceProvider serviceProvider, ILogger<AgentSaga> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;

        InstanceState(x => x.CurrentState);

        Orchestrate = NewState(nameof(Orchestrate));
        ExecuteTools = NewState(nameof(ExecuteTools));
        FollowUpLLM = NewState(nameof(FollowUpLLM));
        Complete = NewState(nameof(Complete));

        Event(() => AgentCallQueued, cfg => cfg.CorrelateById(x => x.Message.CorrelationId));
        Event(() => LLMDispatchRequested, cfg => cfg.CorrelateById(x => x.Message.CorrelationId));
        Event(() => LLMResponseReceived, cfg => cfg.CorrelateById(x => x.Message.CorrelationId));
        Event(() => ToolCallRequested, cfg => cfg.CorrelateById(x => x.Message.CorrelationId));
        Event(() => ToolCallCompleted, cfg => cfg.CorrelateById(x => x.Message.CorrelationId));
        Event(() => LLMFollowUpRequested, cfg => cfg.CorrelateById(x => x.Message.CorrelationId));
        Event(() => NarrativeReady, cfg => cfg.CorrelateById(x => x.Message.CorrelationId));

        // AgentCallQueued → Orchestrate (initiate saga)
        TransitionTo(Orchestrate)
            .When(AgentCallQueued)
            .ThenAsync(async ctx =>
            {
                ctx.Saga.AgentCallId = ctx.Message.SagaId;
                ctx.Saga.GameId = ctx.Message.GameId;
                ctx.Saga.CorrelationId = ctx.Message.CorrelationId != null ? Guid.Parse(ctx.Message.CorrelationId) : Guid.NewGuid();
                ctx.Saga.CurrentState = Orchestrate.Name;
                ctx.Saga.CreatedAt = DateTime.UtcNow;
            })
            .Publish(ctx => new LLMDispatchRequested(
                ctx.Message.SagaId, ctx.Message.GameId,
                "You are the Game Master for a TTRPG session.",
                "Continue the narrative.", null))
            .TransitionTo(Orchestrate);

        // Orchestrate → ExecuteTools (LLM response with tool calls)
        When<LLMResponseReceived>(x =>
        {
            x.Match(
                ctx => ctx.Message.HasToolCalls,
                x.TransitionTo(ExecuteTools)
                    .ThenAsync(async ctx =>
                    {
                        ctx.Saga.CurrentState = ExecuteTools.Name;
                        using var scope = _serviceProvider.CreateScope();
                        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                        var call = await context.AgentCalls.FindAsync(ctx.Saga.Id, ctx.CancellationToken);
                        if (call != null) call.CurrentStep = SagaStep.ToolCallRequested;
                        await context.SaveChangesAsync(ctx.CancellationToken);
                    })
                    .Publish(ctx =>
                    {
                        var tools = ExtractToolCalls(ctx.Saga, ctx.Message.Response);
                        var firstTool = tools.FirstOrDefault();
                        return new ToolCallRequested(
                            ctx.Saga.AgentCallId!.Value, ctx.Saga.GameId!.Value,
                            0, firstTool?.Name ?? "unknown", firstTool?.Arguments ?? "{}");
                    })
                    .TransitionTo(ExecuteTools));

            x.Match(
                ctx => !ctx.Message.HasToolCalls,
                x.TransitionTo(FollowUpLLM)
                    .ThenAsync(ctx =>
                    {
                        ctx.Saga.CurrentState = FollowUpLLM.Name;
                        return Task.CompletedTask;
                    })
                    .Publish(ctx => new NarrativeReady(
                        ctx.Saga.AgentCallId!.Value, ctx.Saga.GameId!.Value,
                        ctx.Message.Response))
                    .Finalize());
        });

        // ExecuteTools → ExecuteTools (more tools) or FollowUpLLM (all tools done)
        When<ToolCallCompleted>(x =>
        {
            x.When(async context =>
            {
                using var scope = _serviceProvider.CreateScope();
                var contextDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var coordinator = await contextDb.ToolCallCoordinators
                    .FirstOrDefaultAsync(c => c.SagaId == context.Saga.AgentCallId && c.Status == CoordinatorStatus.Active, context.CancellationToken);
                return coordinator != null && coordinator.CurrentIndex < coordinator.TotalTools;
            })
            .ThenAsync(async ctx =>
            {
                ctx.Saga.CurrentState = ExecuteTools.Name;
                using var scope = _serviceProvider.CreateScope();
                var contextDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var coordinator = await contextDb.ToolCallCoordinators
                    .FirstOrDefaultAsync(c => c.SagaId == ctx.Saga.AgentCallId && c.Status == CoordinatorStatus.Active, ctx.CancellationToken);
                if (coordinator != null)
                {
                    var tools = JsonSerializer.Deserialize<List<ToolCallInfo>>(coordinator.ToolsJson) ?? new();
                    if (coordinator.CurrentIndex < tools.Count)
                    {
                        var nextTool = tools[coordinator.CurrentIndex];
                        ctx.Publish(new ToolCallRequested(
                            ctx.Saga.AgentCallId!.Value, ctx.Saga.GameId!.Value,
                            coordinator.CurrentIndex, nextTool.Name, nextTool.Arguments));
                    }
                }
            });

            x.Finalize()
                .ThenAsync(async ctx =>
                {
                    ctx.Saga.CurrentState = FollowUpLLM.Name;
                    using var scope = _serviceProvider.CreateScope();
                    var contextDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var coordinator = await contextDb.ToolCallCoordinators
                        .FirstOrDefaultAsync(c => c.SagaId == ctx.Saga.AgentCallId && c.Status == CoordinatorStatus.Active, ctx.CancellationToken);
                    if (coordinator != null)
                    {
                        coordinator.Status = CoordinatorStatus.Completed;
                        coordinator.CompletedAt = DateTime.UtcNow;
                        await contextDb.SaveChangesAsync(ctx.CancellationToken);
                    }
                })
                .Publish(ctx => new LLMFollowUpRequested(
                    ctx.Saga.AgentCallId!.Value, ctx.Saga.GameId!.Value,
                    GetToolResults(ctx.Saga.AgentCallId!.Value)));
        });

        // LLMFollowUpRequested → Complete (follow-up LLM called, narrative ready)
        When<LLMFollowUpRequested>(x =>
        {
            x.TransitionTo(FollowUpLLM)
                .ThenAsync(ctx =>
                {
                    ctx.Saga.CurrentState = FollowUpLLM.Name;
                    return Task.CompletedTask;
                })
                .Publish(ctx => new NarrativeReady(
                    ctx.Saga.AgentCallId!.Value, ctx.Saga.GameId!.Value,
                    ctx.Message.ToolResults.Any()
                        ? $"Tool results:\n{string.Join("\n", ctx.Message.ToolResults.Select(tr => $"Tool '{tr.ToolName}': {tr.Result}"))}"
                        : "Continue the narrative."))
                .Finalize();
        });

        // NarrativeReady → Complete
        When<NarrativeReady>(x =>
        {
            x.TransitionTo(Complete)
                .ThenAsync(ctx =>
                {
                    ctx.Saga.CurrentState = Complete.Name;
                    ctx.Saga.CurrentToolId = null;
                    return Task.CompletedTask;
                });
        });
    }

    private List<ToolCallInfo> ExtractToolCalls(AgentSagaData saga, string response)
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

    private async Task<List<ToolResult>> GetToolResults(Guid sagaId)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var coordinator = await context.ToolCallCoordinators
            .FirstOrDefaultAsync(c => c.SagaId == sagaId && c.Status == CoordinatorStatus.Active);
        if (coordinator == null) return new();
        return coordinator.CompletedTools
            .OrderBy(t => t.Index)
            .Select(t => new ToolResult(t.Index, t.ToolName, t.Result, t.Error))
            .ToList();
    }
}
