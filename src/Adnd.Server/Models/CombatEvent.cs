using System.Text.Json;

namespace Adnd.Server.Models;

public class CombatEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CombatId { get; set; }
    public int Round { get; set; }
    public int Turn { get; set; }
    public CombatEventType EventType { get; set; }
    public Guid? ActorId { get; set; }
    public Guid? TargetId { get; set; }
    public string? Content { get; set; }
    public JsonElement Metadata { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Combat Combat { get; set; } = null!;
}
