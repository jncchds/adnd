using Adnd.Server.Events;
using Adnd.Server.Models;
using MassTransit;

namespace Adnd.Server.Services;

/// <summary>
/// Saga state machine for agent call orchestration.
/// Manages the lifecycle: Orchestrate → (ExecuteTools | FollowUpLLM) → Complete.
/// Persists state in AgentSagaData table — survives container restarts.
/// </summary>
public class AgentSaga : MassTransitStateMachine<AgentSagaData>
{
    public State Orchestrate { get; private set; } = null!;
    public State ExecuteTools { get; private set; } = null!;
    public State FollowUpLLM { get; private set; } = null!;
    public State Complete { get; private set; } = null!;

    public Event<AgentCallQueued> AgentCallQueued { get; private set; } = null!;
    public Event<LLMResponseReceived> LLMResponseReceived { get; private set; } = null!;
    public Event<ToolCallCompleted> ToolCallCompleted { get; private set; } = null!;

    public AgentSaga()
    {
        InstanceState(x => x.CurrentState);

        Event(() => AgentCallQueued, cfg =>
        {
            cfg.CorrelateById(ctx => ctx.Message.SagaId);
            cfg.ConfigureConsumeTopology = false;
        });
        Event(() => LLMResponseReceived, cfg =>
        {
            cfg.CorrelateById(ctx => ctx.Message.SagaId);
            cfg.ConfigureConsumeTopology = false;
        });
        Event(() => ToolCallCompleted, cfg =>
        {
            cfg.CorrelateById(ctx => ctx.Message.SagaId);
            cfg.ConfigureConsumeTopology = false;
        });

        Initially()
            .When(AgentCallQueued, x => x.TransitionTo(Orchestrate));

        During(Orchestrate,
            When(LLMResponseReceived)
                .If(ctx => ctx.Message.HasToolCalls,
                    then: ctx => ctx.TransitionTo(ExecuteTools))
                .Else(ctx => ctx.TransitionTo(FollowUpLLM))
        );

        During(ExecuteTools,
            When(ToolCallCompleted)
                .If(ctx => ctx.Instance.ToolsRemaining > 1,
                    then: ctx => ctx.TransitionTo(ExecuteTools))
                .Else(ctx => ctx.TransitionTo(FollowUpLLM))
        );

        During(FollowUpLLM,
            When(LLMResponseReceived)
                .TransitionTo(Complete)
        );

        SetCompletedWhenFinalized();
    }
}
