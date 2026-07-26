namespace Adnd.Server.Models;

public enum GameSessionStatus { Active, Closed }

public class GameSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GameId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public GameSessionStatus Status { get; set; } = GameSessionStatus.Active;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ClosedAt { get; set; }

    public Game Game { get; set; } = null!;
    public ICollection<Message> Messages { get; set; } = [];
}
