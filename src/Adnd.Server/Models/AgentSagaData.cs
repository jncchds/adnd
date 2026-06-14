using MassTransit;

namespace Adnd.Server.Models;

/// <summary>
/// Persisted state for the Agent saga (MassTransit EF Core repository via SagaStateMachineInstance).
/// Survives container restarts — the saga resumes from this state.
/// </summary>
public class AgentSagaData : SagaStateMachineInstance
{
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
}
