using Adnd.Server.Events;
using Wolverine;

namespace Adnd.Server.Services;

/// <summary>
/// Wolverine saga for agent call orchestration.
/// Manages the lifecycle: Orchestrate → (ExecuteTools | FollowUpLLM) → Complete.
/// State is persisted in the AgentSaga table via Wolverine's RDBMS transport — survives container restarts.
/// </summary>
public class AgentSaga : Saga
{
    // ── State properties (persisted by Wolverine via EF Core) ──
    public Guid Id { get; set; }
    public Guid? AgentCallId { get; set; }
    public Guid? GameId { get; set; }
    public int ToolsRemaining { get; set; }
    public string? CurrentToolId { get; set; }
    public string? CurrentState { get; set; }
    public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;

    // ── Start: triggered by AgentCallQueued ──
    public static async Task<AgentSaga> Start(
        AgentCallQueued message,
        IMessageBus bus,
        ILogger<AgentSaga> logger)
    {
        logger.LogInformation("[SAGA] Starting | SagaId={SagaId} | GameId={GameId}",
            message.SagaId, message.GameId);

        var saga = new AgentSaga
        {
            Id = message.SagaId,
            AgentCallId = message.SagaId,
            GameId = message.GameId,
            CurrentState = "Orchestrate",
            CreatedAt = DateTime.UtcNow
        };

        // Schedule timeout 5 minutes from now
        await bus.ScheduleAsync(
            new ToolCallTimeout(message.SagaId, message.GameId),
            TimeSpan.FromMinutes(5));

        return saga;
    }

    // ── Orchestrate → ExecuteTools or FollowUpLLM ──
    public void Handle(LLMResponseReceived message, ILogger<AgentSaga> logger)
    {
        logger.LogInformation("[SAGA] LLMResponse | SagaId={SagaId} | HasToolCalls={HasToolCalls}",
            Id, message.HasToolCalls);

        if (message.HasToolCalls)
        {
            CurrentState = "ExecuteTools";
            ToolsRemaining = message.ToolCallCount;
        }
        else
        {
            CurrentState = "FollowUpLLM";
        }
    }

    // ── ExecuteTools loop ──
    public void Handle(ToolCallCompleted message, ILogger<AgentSaga> logger)
    {
        ToolsRemaining--;
        CurrentState = ToolsRemaining > 0 ? "ExecuteTools" : "FollowUpLLM";
        logger.LogInformation("[SAGA] ToolCompleted | SagaId={SagaId} | Remaining={Remaining}",
            Id, ToolsRemaining);
    }

    // ── Timeout handler ──
    public void Handle(ToolCallTimeout message, ILogger<AgentSaga> logger)
    {
        logger.LogInformation("[SAGA] Timeout | SagaId={SagaId}", Id);
        MarkCompleted();
    }

    // ── "Saga not found" callback ──
    public static void NotFound(ToolCallCompleted message, ILogger<AgentSaga> logger)
    {
        logger.LogWarning("[SAGA] NotFound | ToolCallCompleted for unknown saga {CallId}",
            message.SagaId);
    }
}

/// <summary>
/// Timeout message for saga expiry (e.g., waiting for player confirmation).
/// </summary>
public record ToolCallTimeout(Guid SagaId, Guid GameId);
