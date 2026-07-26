using System.Text.Json;

namespace Adnd.Server.Models;

public class NPC : ISoftDelete
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GameId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Attitude Attitude { get; set; } = Attitude.Neutral;
    public string? Faction { get; set; }

    public JsonElement Attributes { get; set; }
    public JsonElement Skills { get; set; }
    public JsonElement Inventory { get; set; }

    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public Game Game { get; set; } = null!;
}
