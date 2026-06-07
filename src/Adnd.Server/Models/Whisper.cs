namespace Adnd.Server.Models;

/// <summary>
/// Stores whisper message history for persistence and retrieval.
/// Whispers are private messages between players or from GM to specific players.
/// </summary>
public class Whisper
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GameId { get; set; }
    public Game? Game { get; set; }
    public Guid SessionId { get; set; }
    public GameSession? Session { get; set; }

    // Sender
    public Guid FromPlayerId { get; set; }
    public Player? FromPlayer { get; set; }

    // Targets: stored as comma-separated or JSON array
    // "all" = group whisper to all players
    // "player:{userId}" = targeted whisper
    // "group:{groupName}" = group whisper to named group
    public string Targets { get; set; } = string.Empty;
    public List<Guid> TargetPlayerIds { get; set; } = new();

    // Content
    public string Content { get; set; } = string.Empty;
    public WhisperType Type { get; set; } = WhisperType.PlayerToPlayer;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum WhisperType
{
    PlayerToPlayer, // Player whispers to another player
    PlayerToGM,     // Player whispers to GM
    GMToPlayer,     // GM whispers to a player
    GMToGroup,      // GM whispers to a group of players
    GMToAll         // GM whispers to all players
}
