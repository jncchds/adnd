using System.Text.Json;

namespace Adnd.Server.Models;

/// <summary>
/// Represents a tool call requested by the GM agent (LLM) during gameplay.
/// Some tools auto-execute, others require player/GM confirmation.
/// </summary>
public class GMToolCall
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GameId { get; set; }
    public Game? Game { get; set; }
    public Guid? SessionId { get; set; }
    public GameSession? Session { get; set; }

    // Tool identification
    public string ToolName { get; set; } = string.Empty;
    public string? ToolCallId { get; set; }  // OpenAI-style tool call ID
    public string? Arguments { get; set; }    // JSON arguments

    // Execution status
    public ToolCallStatus Status { get; set; } = ToolCallStatus.Pending;
    public string? Result { get; set; }       // JSON result from execution
    public string? OutputMessage { get; set; } // Human-readable output for chat
    public string? Error { get; set; }

    // Confirmation (for tools requiring user input)
    public bool RequiresConfirmation { get; set; }
    public Guid? ConfirmedBy { get; set; }
    public Player? ConfirmedByPlayer { get; set; }
    public DateTime? ConfirmedAt { get; set; }

    // Timing
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int DurationMs { get; set; }

    // Expiration: tool calls waiting confirmation expire after this duration
    public TimeSpan ExpirationTime { get; set; } = TimeSpan.FromMinutes(5);
    public bool IsExpired => RequiresConfirmation && CompletedAt == null && 
        CreatedAt.Add(ExpirationTime) < DateTime.UtcNow;

    // Parent tool call chain
    public Guid? ParentToolCallId { get; set; }
    public GMToolCall? ParentToolCall { get; set; }
    public ICollection<GMToolCall> ChildToolCalls { get; set; } = new List<GMToolCall>();
}

public enum ToolCallStatus
{
    Pending,
    WaitingConfirmation,
    Confirmed,
    Executing,
    Completed,
    Failed,
    Cancelled
}

/// <summary>
/// Schema definition for a GM tool, compatible with OpenAI function calling format.
/// Used to register tools available to the GM agent.
/// </summary>
public class GMToolDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Dictionary<string, object> Parameters { get; set; } = new();
    public bool RequiresConfirmation { get; set; }
    public ToolCategory Category { get; set; }

    public Dictionary<string, object> ToOpenAISchema()
    {
        return new Dictionary<string, object>
        {
            ["type"] = "function",
            ["function"] = new Dictionary<string, object>
            {
                ["name"] = Name,
                ["description"] = Description,
                ["parameters"] = Parameters
            }
        };
    }
}

public enum ToolCategory
{
    Auto,           // Executes without confirmation
    PlayerRoll,     // Requires player to roll
    SystemRoll,     // GM rolls for system/NPCs
    Combat,         // Combat operations
    Narrative,      // Narrative generation
    Query,          // Information queries
    StateManagement // Game state changes
}
