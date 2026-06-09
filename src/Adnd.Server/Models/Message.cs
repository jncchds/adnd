using System.Text.Json;
using System.ComponentModel.DataAnnotations.Schema;

namespace Adnd.Server.Models;

public class Message
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public GameSession? Session { get; set; }
    public Guid? PlayerId { get; set; }
    public Player? Player { get; set; }
    public string Content { get; set; } = string.Empty;
    public MessageType Type { get; set; } = MessageType.InGamePublic;
    public JsonElement Metadata { get; set; } // Dice results, skill checks, etc.
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // OOC flag: OOC messages don't influence game narrative
    public bool IsOOC { get; set; } = false;

    // Whisper fields (only populated for whisper-type messages)
    public Guid? WhisperFromId { get; set; } // Player who sent the whisper
    public Player? WhisperFrom { get; set; }
    public Guid? WhisperToId { get; set; } // Single target player ID
    public Player? WhisperTo { get; set; }
    public string? WhisperTarget { get; set; } // "all", "player:{userId}", "group:{groupName}"

    // PGVector embedding (stored as JSON text, converted by RAG service)
    [NotMapped]
    public float[]? Embedding { get; set; }
}

public enum MessageType
{
    // === In-game messages (influence narrative) ===
    InGamePublic = 0,      // Public chat visible to all — part of game narrative
    InGameWhisper = 1,     // Whisper to GM — adds to GM knowledge, answered in-character

    // === OOC messages (never influence narrative) ===
    OOCPublic = 2,         // Out-of-character public chat
    OOCWhisper = 3,        // Out-of-character whisper to GM (for clarification)

    // === System / meta messages ===
    Action = 4,            // Player action (attack, skill check, etc.)
    Dice = 5,              // Dice roll result
    System = 6,            // System notification
    GM = 7,                // GM narrative message
    AgentCall = 8,         // Agent framework call log
    AgentResponse = 9      // Agent framework response
}
