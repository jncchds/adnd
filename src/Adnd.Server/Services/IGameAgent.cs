using Adnd.Server.Events;
using Adnd.Server.Models;

namespace Adnd.Server.Services;

/// <summary>
/// Per-game game agent that processes events sequentially.
/// Each active game gets its own GameAgent instance.
/// Events are persisted to the database so they survive restarts.
/// </summary>
public interface IGameAgent
{
    /// <summary>
    /// Start the game agent for a game (activates the GM agent).
    /// </summary>
    Task StartAsync(Guid gameId, Guid creatorId);

    /// <summary>
    /// Pause the game agent (stops processing events).
    /// </summary>
    Task PauseAsync(Guid gameId);

    /// <summary>
    /// Resume the game agent (resumes processing events).
    /// </summary>
    Task ResumeAsync(Guid gameId);

    /// <summary>
    /// Enqueue a pending call ID — called by the AgentCallQueued event handler.
    /// The processing loop checks this queue before querying the database.
    /// </summary>
    void EnqueueCall(Guid callId);

    /// <summary>
    /// Get the current status of the game agent.
    /// </summary>
    Task<GMStatus> GetStatusAsync(Guid gameId);

    /// <summary>
    /// Check if this game agent is active.
    /// </summary>
    bool IsActive(Guid gameId);

    /// <summary>
    /// Get all active game agent IDs.
    /// </summary>
    IEnumerable<Guid> GetActiveGameIds();
}

/// <summary>
/// Manages all game agents (per-game).
/// </summary>
public interface IGameAgentManager
{
    /// <summary>
    /// Get or create a game agent for a game.
    /// </summary>
    IGameAgent GetOrCreate(Guid gameId);

    /// <summary>
    /// Remove a game agent (pauses and stops it).
    /// </summary>
    void Remove(Guid gameId);

    /// <summary>
    /// Start all game agents that should be running (called on startup to recover from restart).
    /// </summary>
    Task StartAllActiveGamesAsync();

    /// <summary>
    /// Wake up a GameAgent when a new call is queued — eliminates polling delay.
    /// Called by the AgentCallQueued event handler.
    /// </summary>
    void OnAgentCallQueued(Guid gameId, Guid callId);
}
