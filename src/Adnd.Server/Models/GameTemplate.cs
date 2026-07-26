namespace Adnd.Server.Models;

public class GameTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid? LLMPresetId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string SystemId { get; set; } = "dnd5e";
    public string Language { get; set; } = "English";
    public string? PlotSeed { get; set; }
    public string? GameParameters { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public User User { get; set; } = null!;
}
