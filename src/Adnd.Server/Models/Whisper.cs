using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

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

    // TargetPlayerIds: list of player IDs to whisper to
    // "all" is represented as a special entry with Guid.Empty
    public List<Guid> TargetPlayerIds { get; set; } = new();

    // Computed property for API responses — derived from TargetPlayerIds
    [NotMapped]
    public string Targets
    {
        get
        {
            if (TargetPlayerIds.Count == 0) return "all";
            var sb = new StringBuilder();
            foreach (var id in TargetPlayerIds)
            {
                if (sb.Length > 0) sb.Append(',');
                sb.Append(id == Guid.Empty ? "all" : $"player:{id}");
            }
            return sb.ToString();
        }
    }

    // Content
    public string Content { get; set; } = string.Empty;
    public WhisperType Type { get; set; } = WhisperType.PlayerToPlayer;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum WhisperType
{
    // === In-game whispers (narrative) ===
    InGamePlayerToGM = 0,   // Player whispers to GM — adds to GM knowledge
    InGameGMToPlayer = 1,   // GM whispers to a player (e.g., divination result)

    // === OOC whispers (non-narrative) ===
    OOCPlayerToGM = 2,      // Player OOC to GM — for clarification
    OOCGMToPlayer = 3,      // GM OOC response to player

    // === Legacy (kept for compatibility) ===
    PlayerToPlayer = 4,     // Player whispers to another player (ignored by GM)
    GMToGroup = 5,          // GM whispers to a group
    GMToAll = 6,            // GM whispers to all players
}
