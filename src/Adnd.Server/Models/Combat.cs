using System.Text.Json;

namespace Adnd.Server.Models;

public class Combat
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GameId { get; set; }
    public Game? Game { get; set; }
    public Guid? SessionId { get; set; }
    public GameSession? Session { get; set; }
    public string? Name { get; set; } // e.g., "Goblin Ambush"
    public CombatStatus Status { get; set; } = CombatStatus.Active;
    public int CurrentRound { get; set; } = 0;
    public int CurrentTurnIndex { get; set; } = 0; // Index into Participants list
    public int InitiativeCount { get; set; } = 0; // Tiebreak counter
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }
    public List<CombatParticipant> Participants { get; set; } = new();
    public List<CombatEvent> Events { get; set; } = new();
    public JsonElement? Notes { get; set; } // JSON: grid settings, custom data
}

public class CombatParticipant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CombatId { get; set; }
    public Combat? Combat { get; set; }
    public string ParticipantType { get; set; } = "Player"; // "Player" or "NPC"
    public Guid? PlayerId { get; set; }
    public Player? Player { get; set; }
    public Guid? NpcId { get; set; }
    public NPC? NPC { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public int Initiative { get; set; }
    public int InitiativeCount { get; set; } // Tiebreak (lower = earlier)
    public int CurrentHP { get; set; }
    public int MaxHP { get; set; }
    public int AC { get; set; }
    public int? InitiativeBonus { get; set; } // Dex mod or other bonus
    public JsonElement Conditions { get; set; } // JSON array of active conditions
    public JsonElement? TemporaryHP { get; set; } // Temp HP separate from current
    public JsonElement? SavingThrows { get; set; } // { "fortitude": 3, "reflex": 1, "will": -1 }
    public JsonElement? DeathSaveState { get; set; } // { "successes": 0, "failures": 0 }
    public JsonElement? Notes { get; set; } // GM notes about this participant
}

public class CombatEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CombatId { get; set; }
    public Combat? Combat { get; set; }
    public int Round { get; set; }
    public int TurnIndex { get; set; }
    public CombatEventType Type { get; set; }
    public string ActorName { get; set; } = string.Empty;
    public string TargetName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public JsonElement? Metadata { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum CombatStatus
{
    Active,
    Paused,
    Finished
}

public enum CombatEventType
{
    CombatStart,
    CombatEnd,
    TurnChange,
    Attack,
    Damage,
    Healing,
    Condition,
    SaveThrow,
    Initiative,
    Death,
    Revival,
    DeathSave,
    RoundStart
}

// ============= Spell types =============

public class SpellEntry
{
    public string Name { get; set; } = string.Empty;
    public string Level { get; set; } = "0";
    public string School { get; set; } = string.Empty;
    public string CastingTime { get; set; } = "1 action";
    public string Range { get; set; } = "Self";
    public string Duration { get; set; } = "Instantaneous";
    public string Components { get; set; } = "V,S";
    public string Description { get; set; } = string.Empty;
    public string? SaveType { get; set; }
    public int? SaveDC { get; set; }
    public string? DamageFormula { get; set; }
    public int? DamageBonus { get; set; }
    public string? DamageType { get; set; }
    public bool IsPrepared { get; set; } = true;
    public bool IsKnown { get; set; } = true;
}

public class SpellSlotInfo
{
    public string Level { get; set; } = "1";
    public int SlotsTotal { get; set; } = 2;
    public int SlotsRemaining { get; set; } = 2;
    public int? SlotsUsed { get; set; } = 0;
}

public class SpellCastResult
{
    public string Caster { get; set; } = string.Empty;
    public string SpellName { get; set; } = string.Empty;
    public string SpellLevel { get; set; } = "0";
    public string Target { get; set; } = string.Empty;
    public string SaveType { get; set; } = string.Empty;
    public int SaveDC { get; set; }
    public bool SaveSuccess { get; set; }
    public bool IsCritical { get; set; }
    public string DamageType { get; set; } = string.Empty;
    public int DamageTotal { get; set; }
    public string DamageInfo { get; set; } = string.Empty;
    public string Effect { get; set; } = string.Empty;
    public int? TargetHP { get; set; }
    public int? TargetMaxHP { get; set; }
    public DateTime RolledAt { get; set; }
}

// ============= Grid/Map types =============

public class GridPosition
{
    public Guid ParticipantId { get; set; }
    public int GridX { get; set; }
    public int GridY { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public int MoveSpeed { get; set; } = 30;
    public string? Status { get; set; }
}

// ============= AI Combat types =============

public class AICombatSuggestion
{
    public List<AITacticalAction> Suggestions { get; set; } = new();
    public string ThreatLevel { get; set; } = "Low";
    public string RecommendedStrategy { get; set; } = string.Empty;
    public List<AINPCAction> NPCActions { get; set; } = new();
    public List<AICombatWarning> Warnings { get; set; } = new();
    public string GeneratedAt { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
}

public class AITacticalAction
{
    public string Actor { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public int Priority { get; set; } = 0;
    public string? Details { get; set; }
}

public class AINPCAction
{
    public string NPCName { get; set; } = string.Empty;
    public string Behavior { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

public class AICombatWarning
{
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = "Medium";
    public string? AffectedParticipant { get; set; }
}

// ============= Rest types =============

public class RestResult
{
    public string RestType { get; set; } = string.Empty;
    public bool IsInProgress { get; set; }
    public int RoundsRemaining { get; set; } = 0;
    public int HPRecovered { get; set; } = 0;
    public int MaxHPRecovered { get; set; } = 0;
    public int SpellSlotsRecovered { get; set; } = 0;
    public List<string> Effects { get; set; } = new();
}

// ============= Sanity types (CoC) =============

public class SanityCheckResult
{
    public string Participant { get; set; } = string.Empty;
    public int CurrentSAN { get; set; }
    public int Roll { get; set; }
    public int DC { get; set; }
    public bool Success { get; set; }
    public bool IsCritical { get; set; }
    public int SANLoss { get; set; }
    public string Effect { get; set; } = string.Empty;
    public DateTime RolledAt { get; set; }
}

// ============= Equipment types =============

public class EquipmentEntry
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public int? Weight { get; set; }
    public string? Cost { get; set; }
    public string? Damage { get; set; }
    public string? DamageType { get; set; }
    public string? Properties { get; set; }
    public string? ACBonus { get; set; }
    public string? Description { get; set; }
    public bool IsEquipped { get; set; } = false;
    public int? ACBonusValue { get; set; }
}

// ============= XP/Leveling types =============

public class XPEntry
{
    public int Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}

public class LevelUpResult
{
    public string Participant { get; set; } = string.Empty;
    public int OldLevel { get; set; }
    public int NewLevel { get; set; }
    public int NewMaxHP { get; set; }
    public int NewProficiencyBonus { get; set; }
    public List<string> NewAbilities { get; set; } = new();
    public List<string> AbilityChoices { get; set; } = new();
}

// ============= Combat action result types =============

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
