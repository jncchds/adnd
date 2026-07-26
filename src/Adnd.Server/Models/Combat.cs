using System.Text.Json;

namespace Adnd.Server.Models;

public class Combat
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GameId { get; set; }
    public Guid SessionId { get; set; }
    public string Name { get; set; } = "Combat";
    public CombatStatus Status { get; set; } = CombatStatus.Active;
    public int CurrentRound { get; set; } = 1;
    public int CurrentTurnIndex { get; set; }
    public float InitiativeCount { get; set; }
    public JsonElement Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<CombatParticipant> Participants { get; set; } = [];
    public ICollection<CombatEvent> Events { get; set; } = [];
}
