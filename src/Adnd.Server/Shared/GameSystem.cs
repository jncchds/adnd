namespace Adnd.Server.Shared;

public class GameSystem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public SystemType Type { get; set; }
    public string? RulesetConfig { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public enum SystemType
{
    Predefined = 0,
    Custom = 1
}
