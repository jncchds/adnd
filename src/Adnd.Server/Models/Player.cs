namespace Adnd.Server.Models;

public class Player : ISoftDelete
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GameId { get; set; }
    public Guid UserId { get; set; }
    public string CharacterName { get; set; } = string.Empty;
    public PlayerRole Role { get; set; } = PlayerRole.Player;
    public PlayerStatus Status { get; set; } = PlayerStatus.Active;
    public bool IsConnected { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public Game Game { get; set; } = null!;
    public User User { get; set; } = null!;
    public Character? Character { get; set; }
}
