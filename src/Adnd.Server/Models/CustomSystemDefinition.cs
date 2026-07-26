using System.Text.Json;

namespace Adnd.Server.Models;

public class CustomSystemDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GameId { get; set; }
    public JsonElement Definition { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Game Game { get; set; } = null!;
}
