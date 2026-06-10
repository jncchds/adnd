namespace Adnd.Server.Shared;

public class Game
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public Guid SystemId { get; set; }
    public GameSystem? System { get; set; }
    public Guid CreatorId { get; set; }
    public User? Creator { get; set; }
    public GameStatus Status { get; set; }
    public string? PlotSeed { get; set; }
    public string JoinCode { get; set; } = string.Empty;
    public Guid? CurrentSessionId { get; set; }
    public ICollection<GamePlayer> GamePlayers { get; set; } = new List<GamePlayer>();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public enum GameStatus
{
    Draft = 0,
    Active = 1,
    Archived = 2
}
