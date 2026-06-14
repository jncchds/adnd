namespace Adnd.Server.Models;

/// <summary>
/// Persisted state for the Agent saga (MassTransit EF Core repository).
/// Survives container restarts — the saga resumes from this state.
/// Phase 2e: saga registration added after consumers are migrated.
/// NOTE: ISagaStateMachineInstance is internal in MassTransit 8.x — saga data class
/// just needs Guid CorrelationId property; MassTransit runtime handles the rest.
/// </summary>
public class AgentSagaData
{
    public Guid Id { get; set; }

    /// <summary>
    /// Current state name (MassTransit state machine state).
    /// </summary>
    public string CurrentState { get; set; } = string.Empty;

    /// <summary>
    /// The AgentCall entity ID this saga is processing.
    /// </summary>
    public Guid? AgentCallId { get; set; }

    /// <summary>
    /// Game ID this saga belongs to.
    /// </summary>
    public Guid? GameId { get; set; }

    /// <summary>
    /// Number of tools remaining to execute.
    /// </summary>
    public int ToolsRemaining { get; set; }

    /// <summary>
    /// ID of the current tool being executed (if any).
    /// </summary>
    public string? CurrentToolId { get; set; }

    /// <summary>
    /// Correlation ID for the agent call (required by MassTransit saga).
    /// </summary>
    public Guid CorrelationId { get; set; }

    /// <summary>
    /// Timestamp when the saga was created.
    /// </summary>
    public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Concurrency stamp for EF Core saga persistence.
    /// </summary>
    public int Version { get; set; }
}
