namespace Adnd.Server.Models;

public class Player : ISoftDelete
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GameId { get; set; }
    public Game? Game { get; set; }
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public string CharacterName { get; set; } = string.Empty;
    public PlayerRole Role { get; set; } = PlayerRole.Player;
    public PlayerStatus Status { get; set; } = PlayerStatus.Active;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LeftAt { get; set; }

    // Soft-delete support
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }

    // Whisper settings
    public bool CanWhisper { get; set; } = true;
    public List<string> WhisperGroups { get; set; } = new(); // e.g., "party", "stealth"

    public Character? Character { get; set; }
}

public enum PlayerRole
{
    Creator, // The game creator (has admin-level control)
    Player,
    Spectator,
    Observer // Can watch but not play
}

public enum PlayerStatus
{
    Active,
    Left
}
