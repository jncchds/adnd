using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Adnd.Server.Data;
using Adnd.Server.Models;

namespace Adnd.Server.Services;

public partial class CombatService
{
    // ==================== Helpers ====================

    private Dictionary<string, object>? GetNotes(CombatParticipant participant)
    {
        if (participant.Notes.HasValue &&
            participant.Notes.Value.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            var notes = participant.Notes;
            return JsonSerializer.Deserialize<Dictionary<string, object>>(notes.ToString());
        }
        return null;
    }

    private Dictionary<string, int> GetSavingThrows(CombatParticipant participant)
    {
        if (participant.SavingThrows.HasValue &&
            participant.SavingThrows.Value.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            var sv = participant.SavingThrows;
            return JsonSerializer.Deserialize<Dictionary<string, int>>(sv.ToString())
                ?? new Dictionary<string, int>();
        }
        return new Dictionary<string, int>();
    }
}

// ============= DTOs =============

public class AttackWithCombatResult
{
    public string Attacker { get; set; } = string.Empty;
    public string Weapon { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public bool Hit { get; set; }
    public bool IsCritical { get; set; }
    public bool IsFumble { get; set; }
    public int AttackRoll { get; set; }
    public int AttackDice { get; set; }
    public int AC { get; set; }
    public string DamageDice { get; set; } = string.Empty;
    public int DamageTotal { get; set; }
    public string DamageInfo { get; set; } = string.Empty;
    public int TargetHP { get; set; }
    public int TargetMaxHP { get; set; }
}

public class SaveThrowResult
{
    public string Participant { get; set; } = string.Empty;
    public string SaveType { get; set; } = string.Empty;
    public int DiceRoll { get; set; }
    public int DC { get; set; }
    public bool Success { get; set; }
    public DateTime RolledAt { get; set; }
}

public class DeathSaveResult
{
    public string Participant { get; set; } = string.Empty;
    public bool Success { get; set; }
    public int Successes { get; set; }
    public int Failures { get; set; }
    public bool IsStabilized { get; set; }
    public bool IsDead { get; set; }
    public DateTime RolledAt { get; set; }
}

public class CombatLog
{
    public Guid CombatId { get; set; }
    public string? Name { get; set; }
    public CombatStatus Status { get; set; }
    public int CurrentRound { get; set; }
    public int CurrentTurnIndex { get; set; }
    public List<ParticipantSummary> Participants { get; set; } = new();
    public List<CombatLogEvent> Events { get; set; } = new();
}

public class ParticipantSummary
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string ParticipantType { get; set; } = string.Empty;
    public int CurrentHP { get; set; }
    public int MaxHP { get; set; }
    public int AC { get; set; }
    public int Initiative { get; set; }
    public List<ConditionEntry> Conditions { get; set; } = new();
    public bool IsCurrentTurn { get; set; }
    public bool IsDead { get; set; }
}

public class ConditionEntry
{
    public string Name { get; set; } = string.Empty;
    public int Duration { get; set; }
    public string? Description { get; set; }
}

public class DeathSaveState
{
    public int Successes { get; set; }
    public int Failures { get; set; }
}

public class CombatLogEvent
{
    public Guid Id { get; set; }
    public int Round { get; set; }
    public int TurnIndex { get; set; }
    public CombatEventType Type { get; set; }
    public string ActorName { get; set; } = string.Empty;
    public string TargetName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public JsonElement? Metadata { get; set; }
    public DateTime CreatedAt { get; set; }
}
