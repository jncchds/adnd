namespace Adnd.Server.Models;

public class CustomSystemDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GameId { get; set; }
    public Game? Game { get; set; }
    public string Name { get; set; } = string.Empty;
    public string JsonDefinition { get; set; } = string.Empty; // The JSON schema
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
