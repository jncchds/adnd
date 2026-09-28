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

    /// <summary>
    /// The named areas the fight takes place in — "Bridge", "Riverbank", "Tower door".
    /// Position is which zone a combatant is in, not a square: same zone is melee reach,
    /// an adjacent zone is one move. See <see cref="Services.ICombatTracker"/>.
    /// </summary>
    public List<CombatZone> Zones { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<CombatParticipant> Participants { get; set; } = [];
    public ICollection<CombatEvent> Events { get; set; } = [];
}

public class CombatZone
{
    /// <summary>Lower-cased name. Zones are addressed by name, since the model names them in
    /// narration and would have to invent an id.</summary>
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    /// <summary>Free-form terrain notes the narrator rules on: "difficult", "cover", "elevated".</summary>
    public List<string> Tags { get; set; } = [];
}
