namespace Adnd.Server.Shared;

public class GamePlayer
{
    public Guid Id { get; set; }
    public Guid GameId { get; set; }
    public Game? Game { get; set; }
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public PlayerRole Role { get; set; }
    public string CharacterName { get; set; } = string.Empty;
    public bool Spectating { get; set; }
    public bool IsBanned { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public enum PlayerRole
{
    Creator = 0,
    Gm = 1,
    Player = 2
}
