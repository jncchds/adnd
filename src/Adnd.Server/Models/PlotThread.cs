namespace Adnd.Server.Models;

public class PlotThread
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GameId { get; set; }
    public Game? Game { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public PlotThreadStatus Status { get; set; } = PlotThreadStatus.Active;
    public List<Guid> KeyEventMessageIds { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // PGVector embedding
    public float[]? Embedding { get; set; }
}

public enum PlotThreadStatus
{
    Active,
    Resolved,
    Abandoned
}
