using System.Text.Json;

namespace Adnd.Server.Models;

public class Message
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public GameSession? Session { get; set; }
    public Guid? PlayerId { get; set; }
    public Player? Player { get; set; }
    public string Content { get; set; } = string.Empty;
    public MessageType Type { get; set; } = MessageType.Chat;
    public JsonElement Metadata { get; set; } // Dice results, skill checks, etc.
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Whisper fields
    public Guid? WhisperFromId { get; set; } // Player who sent the whisper
    public Player? WhisperFrom { get; set; }
    public string? WhisperTarget { get; set; } // "all", "player:{userId}", "group:{groupName}"

    // PGVector embedding
    public float[]? Embedding { get; set; }
}

public enum MessageType
{
    Chat,
    Action,
    Dice,
    System,
    GM,
    PlayerWhisper,    // Player-to-player whisper
    GMWhisper,        // GM-to-player(s) whisper
    AgentCall,        // Agent framework call log
    AgentResponse     // Agent framework response
}
