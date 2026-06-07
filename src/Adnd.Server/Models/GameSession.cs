namespace Adnd.Server.Models;

public class GameSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GameId { get; set; }
    public Game? Game { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? PlotNodes { get; set; } // JSON: story node tree
    public string? GameState { get; set; } // JSON: current state
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }

    public ICollection<Message> Messages { get; set; } = new List<Message>();
}
