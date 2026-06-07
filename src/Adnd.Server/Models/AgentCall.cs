using System.Text.Json;

namespace Adnd.Server.Models;

/// <summary>
/// Represents an agent-to-agent call in the agentic framework.
/// Agents can call each other to receive necessary information to continue the game.
/// </summary>
public class AgentCall
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GameId { get; set; }
    public Game? Game { get; set; }
    public Guid? SessionId { get; set; }
    public GameSession? Session { get; set; }

    // Caller and target
    public AgentType FromAgent { get; set; } = AgentType.GM;
    public AgentType ToAgent { get; set; } = AgentType.LLM;

    // What action to perform
    public AgentAction Action { get; set; } = AgentAction.Query;

    // Input/output data
    public string? Input { get; set; }       // JSON: input context/data
    public string? Output { get; set; }      // JSON: result/output
    public string? OutputMessage { get; set; } // Human-readable output for chat

    // Status tracking
    public AgentCallStatus Status { get; set; } = AgentCallStatus.Pending;
    public string? Error { get; set; }

    // Timing
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int DurationMs { get; set; }

    // Metadata
    public JsonElement? Metadata { get; set; }

    // Parent call (for chained calls)
    public Guid? ParentCallId { get; set; }
    public AgentCall? ParentCall { get; set; }
    public ICollection<AgentCall> ChildCalls { get; set; } = new List<AgentCall>();
}

public enum AgentType
{
    Creator,  // Game creator (sends narrative nudges)
    GM,       // Game Master orchestrator (LLM-driven)
    LLM,      // Language model (narrative, suggestions)
    Dice,     // Dice engine
    RAG,      // Retrieval-augmented generation (consistency, context)
    NPC,      // NPC behavior engine
    Player,   // Player assistance (character sheets, rules)
    System    // System rules engine
}

public enum AgentAction
{
    Query,       // Ask a question / request info
    Generate,    // Generate content (narrative, dialogue)
    Roll,        // Roll dice
    Check,       // Check consistency / validate
    Narrate,     // Narrate an event
    Suggest,     // Suggest actions / plot continuations
    Execute,     // Execute an action (attack, skill check)
    Notify,      // Notify other agents of an event
    Recall,      // Recall relevant context
    ManageState,  // Update game state
    Nudge         // Creator's narrative direction
}

public enum AgentCallStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled
}
