using System.Text.Json;

namespace Adnd.Server.Models;

public class NPC : ISoftDelete
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GameId { get; set; }
    public Game? Game { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public JsonElement Attributes { get; set; }
    public JsonElement Skills { get; set; }
    public JsonElement Inventory { get; set; }
    public JsonElement Spells { get; set; }
    public Guid? PlotThreadId { get; set; }
    public PlotThread? PlotThread { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Soft-delete support
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
}
