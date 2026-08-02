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
    public NPCStatus Status { get; set; } = NPCStatus.Active;

    /// <summary>
    /// When the GM last registered, updated or otherwise touched this NPC. Drives which NPCs
    /// are worth putting in front of the narrator: a campaign accumulates far more NPCs than
    /// fit in a prompt, and recency is the cheapest usable proxy for "still in play".
    /// </summary>
    public DateTimeOffset? LastSeenAt { get; set; }

    public JsonElement Attributes { get; set; }
    public JsonElement Skills { get; set; }
    public JsonElement Inventory { get; set; }

    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public Game Game { get; set; } = null!;
}
