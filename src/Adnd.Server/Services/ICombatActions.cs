using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Adnd.Server.Data;
using Adnd.Server.Models;

namespace Adnd.Server.Services;

/// <summary>
/// Factory pattern for combat actions. Each action type (Attack, Save, Spell)
/// has its own strategy with system-specific logic delegated to ISystemRules.
/// </summary>
public interface ICombatActionFactory
{
    ICombatAction GetAction(CombatActionType actionType);
    void RegisterAction(CombatActionType actionType, ICombatAction action);
}

public enum CombatActionType
{
    Attack,
    SaveThrow,
    Spell,
}

public interface ICombatAction
{
    string ActionName { get; }
    Task<object> ExecuteAsync(Combat combat, Guid combatId, AppDbContext context, IDiceEngine diceEngine,
        ISystemRules rules, ILogger logger);
}

/// <summary>
/// Concrete action for executing an attack.
/// </summary>
public class AttackAction : ICombatAction
{
    public string ActionName => "Attack";

    public Task<object> ExecuteAsync(Combat combat, Guid combatId, AppDbContext context, IDiceEngine diceEngine,
        ISystemRules rules, ILogger logger)
    {
        throw new NotImplementedException("Use ExecuteAsync overload with parameters.");
    }

    public static async Task<AttackWithCombatResult> ExecuteAsync(Combat combat, string attackerName, string weapon,
        Guid targetId, string attackFormula, int? attackBonus, string? damageFormula,
        int? damageBonus, string? description, AppDbContext context, IDiceEngine diceEngine,
        ISystemRules rules, ILogger logger)
    {
        var target = combat.Participants.FirstOrDefault(p => p.Id == targetId)
            ?? throw new KeyNotFoundException($"Target participant {targetId} not found in combat.");

        var attackResult = diceEngine.Roll(attackFormula);
        var totalAttack = attackResult.Total + (attackBonus ?? 0);
        var hit = totalAttack >= target.AC;

        var isCrit = rules.IsCriticalHit(attackResult.Rolls.Length == 1 ? attackResult.Rolls[0] : 20, combat.GameId.ToString());
        var isFumble = rules.IsFumble(attackResult.Rolls.Length == 1 ? attackResult.Rolls[0] : 1, combat.GameId.ToString());

        string damageInfo = string.Empty;
        int damageTotal = 0;

        if (hit)
        {
            if (isCrit && damageFormula != null)
            {
                var critDamage = diceEngine.Roll(damageFormula);
                damageTotal = critDamage.Rolls.Concat(critDamage.Rolls).Sum() + (damageBonus ?? 0);
                damageInfo = $"CRITICAL! {damageFormula} doubled + {damageBonus ?? 0} = {damageTotal}";
            }
            else if (damageFormula != null)
            {
                var dmgResult = diceEngine.Roll(damageFormula);
                damageTotal = dmgResult.Total + (damageBonus ?? 0);
                damageInfo = $"{damageFormula} + {damageBonus ?? 0} = {damageTotal}";
                await HandleDamageAsync(combat, target, damageTotal, attackerName, context);
            }
        }
        else if (isFumble)
        {
            damageInfo = "Fumble! Weapon malfunctioned.";
            await AddCombatEventAsync(combat, combat.CurrentRound, combat.CurrentTurnIndex,
                CombatEventType.Condition, attackerName, attackerName, "Fumbled attack.", context);
        }

        await AddCombatEventAsync(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.Attack, attackerName, target.DisplayName,
            $"{weapon}: {attackFormula}+{attackBonus ?? 0}={totalAttack} vs AC {target.AC} -> {(hit ? "HIT" : "MISS")}"
            + (hit ? $" | {damageInfo}" : ""), context);

        return new AttackWithCombatResult
        {
            Attacker = attackerName,
            Weapon = weapon,
            Target = target.DisplayName,
            Hit = hit,
            IsCritical = isCrit,
            IsFumble = isFumble,
            AttackRoll = totalAttack,
            AttackDice = attackResult.Total,
            AC = target.AC,
            DamageDice = damageFormula ?? string.Empty,
            DamageTotal = damageTotal,
            DamageInfo = damageInfo,
            TargetHP = target.CurrentHP,
            TargetMaxHP = target.MaxHP
        };
    }

    private static async Task HandleDamageAsync(Combat combat, CombatParticipant target, int damage, string source, AppDbContext context)
    {
        var tempHP = target.TemporaryHP?.GetInt32() ?? 0;
        int damageToHP = damage;

        if (tempHP > 0)
        {
            if (damage <= tempHP)
            {
                target.TemporaryHP = JsonDocument.Parse(JsonSerializer.Serialize(0)).RootElement;
                await AddCombatEventAsync(combat, combat.CurrentRound, combat.CurrentTurnIndex,
                    CombatEventType.Damage, source, target.DisplayName,
                    $"Took {damage} damage (absorbed by {damage} temp HP).", context);
                return;
            }
            else
            {
                damageToHP = damage - tempHP;
                target.TemporaryHP = JsonDocument.Parse(JsonSerializer.Serialize(0)).RootElement;
            }
        }

        target.CurrentHP = Math.Max(0, target.CurrentHP - damageToHP);

        await AddCombatEventAsync(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.Damage, source, target.DisplayName,
            $"Took {damageToHP} damage ({target.CurrentHP}/{target.MaxHP} HP remaining).", context);

        if (target.CurrentHP <= 0)
        {
            var ds = GetDeathSaveState(target);
            if (ds.Successes == 0 && ds.Failures == 0)
            {
                target.DeathSaveState = JsonDocument.Parse(JsonSerializer.Serialize(new DeathSaveState { Successes = 0, Failures = 0 })).RootElement;
            }
        }

        context.Combats.Update(combat);
        await context.SaveChangesAsync();
    }

    private static DeathSaveState GetDeathSaveState(CombatParticipant participant)
    {
        if (participant.DeathSaveState.HasValue &&
            participant.DeathSaveState.Value.ValueKind == JsonValueKind.Object)
        {
            return JsonSerializer.Deserialize<DeathSaveState>(participant.DeathSaveState.ToString())
                ?? new DeathSaveState();
        }
        return new DeathSaveState();
    }

    private static async Task AddCombatEventAsync(Combat combat, int round, int turnIndex, CombatEventType type,
        string actorName, string targetName, string content, AppDbContext context)
    {
        var evt = new CombatEvent
        {
            CombatId = combat.Id,
            Round = round,
            TurnIndex = turnIndex,
            Type = type,
            ActorName = actorName,
            TargetName = targetName,
            Content = content
        };
        context.CombatEvents.Add(evt);
        await context.SaveChangesAsync();
    }
}

/// <summary>
/// Concrete action for executing a saving throw.
/// </summary>
public class SaveThrowAction : ICombatAction
{
    public string ActionName => "SaveThrow";

    public Task<object> ExecuteAsync(Combat combat, Guid combatId, AppDbContext context, IDiceEngine diceEngine,
        ISystemRules rules, ILogger logger)
    {
        throw new NotImplementedException("Use ExecuteSaveThrowAsync overload with parameters.");
    }

    public static async Task<SaveThrowResult> ExecuteAsync(Combat combat, string participantName, Guid participantId,
        string saveType, string saveFormula, int dc, AppDbContext context, IDiceEngine diceEngine,
        ISystemRules rules, ILogger logger)
    {
        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant {participantId} not found in combat.");

        var rollResult = diceEngine.Roll(saveFormula);
        var total = rollResult.Total;
        var success = total >= dc;

        await AddCombatEventAsync(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.SaveThrow, participantName, participantName,
            $"{saveType}: {saveFormula}={total} vs DC {dc} -> {(success ? "SUCCESS" : "FAILURE")}", context);

        return new SaveThrowResult
        {
            Participant = participant.DisplayName,
            SaveType = saveType,
            DiceRoll = total,
            DC = dc,
            Success = success,
            RolledAt = DateTime.UtcNow
        };
    }

    private static async Task AddCombatEventAsync(Combat combat, int round, int turnIndex, CombatEventType type,
        string actorName, string targetName, string content, AppDbContext context)
    {
        var evt = new CombatEvent
        {
            CombatId = combat.Id,
            Round = round,
            TurnIndex = turnIndex,
            Type = type,
            ActorName = actorName,
            TargetName = targetName,
            Content = content
        };
        context.CombatEvents.Add(evt);
        await context.SaveChangesAsync();
    }
}

/// <summary>
/// Concrete action for casting a spell.
/// </summary>
public class SpellAction : ICombatAction
{
    public string ActionName => "Spell";

    public Task<object> ExecuteAsync(Combat combat, Guid combatId, AppDbContext context, IDiceEngine diceEngine,
        ISystemRules rules, ILogger logger)
    {
        throw new NotImplementedException("Use ExecuteSpellAsync overload with parameters.");
    }

    public static async Task<SpellCastResult> ExecuteAsync(Combat combat, string casterName, string spellName,
        Guid targetId, string saveFormula, int saveDC, string? damageFormula,
        int? damageBonus, string? description, AppDbContext context, IDiceEngine diceEngine,
        ISystemRules rules, ILogger logger)
    {
        var target = combat.Participants.FirstOrDefault(p => p.Id == targetId)
            ?? throw new KeyNotFoundException($"Target participant {targetId} not found in combat.");

        var saveResult = diceEngine.Roll(saveFormula);
        var saveTotal = saveResult.Total;
        var saveSuccess = saveTotal >= saveDC;

        var isCritSave = saveResult.Rolls.Length == 1 && saveResult.Rolls[0] == 20;
        if (isCritSave) saveSuccess = true;

        var isFumbleSave = saveResult.Rolls.Length == 1 && saveResult.Rolls[0] == 1;
        if (isFumbleSave) saveSuccess = false;

        int damageTotal = 0;
        string damageInfo = string.Empty;
        string effect = string.Empty;

        if (damageFormula != null && !saveSuccess)
        {
            var dmgResult = diceEngine.Roll(damageFormula);
            damageTotal = dmgResult.Total + (damageBonus ?? 0);
            damageInfo = $"{damageFormula} + {damageBonus ?? 0} = {damageTotal}";
            await HandleDamageAsync(combat, target, damageTotal, casterName, context);
        }
        else if (damageFormula != null && saveSuccess)
        {
            var dmgResult = diceEngine.Roll(damageFormula);
            damageTotal = (dmgResult.Total + (damageBonus ?? 0)) / 2;
            damageInfo = $"Half: {damageFormula} + {damageBonus ?? 0} = {damageTotal} (half)";
            await HandleDamageAsync(combat, target, damageTotal, casterName, context);
        }
        else if (description != null)
        {
            effect = description;
        }

        await AddCombatEventAsync(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.Healing, casterName, target.DisplayName,
            $"Cast {spellName}: {saveFormula}={saveTotal} vs DC {saveDC} -> {(saveSuccess ? "SUCCESS" : "FAILURE")}"
            + (damageTotal > 0 ? $" | {damageInfo}" : $" | {effect}"), context);

        return new SpellCastResult
        {
            Caster = casterName,
            SpellName = spellName,
            Target = target.DisplayName,
            SaveType = saveFormula,
            SaveDC = saveDC,
            SaveSuccess = saveSuccess,
            IsCritical = isCritSave,
            DamageTotal = damageTotal,
            DamageInfo = damageInfo,
            Effect = effect,
            TargetHP = target.CurrentHP,
            TargetMaxHP = target.MaxHP,
            RolledAt = DateTime.UtcNow
        };
    }

    private static async Task HandleDamageAsync(Combat combat, CombatParticipant target, int damage, string source, AppDbContext context)
    {
        var tempHP = target.TemporaryHP?.GetInt32() ?? 0;
        int damageToHP = damage;

        if (tempHP > 0)
        {
            if (damage <= tempHP)
            {
                target.TemporaryHP = JsonDocument.Parse(JsonSerializer.Serialize(0)).RootElement;
                await AddCombatEventAsync(combat, combat.CurrentRound, combat.CurrentTurnIndex,
                    CombatEventType.Damage, source, target.DisplayName,
                    $"Took {damage} damage (absorbed by {damage} temp HP).", context);
                return;
            }
            else
            {
                damageToHP = damage - tempHP;
                target.TemporaryHP = JsonDocument.Parse(JsonSerializer.Serialize(0)).RootElement;
            }
        }

        target.CurrentHP = Math.Max(0, target.CurrentHP - damageToHP);

        await AddCombatEventAsync(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.Damage, source, target.DisplayName,
            $"Took {damageToHP} damage ({target.CurrentHP}/{target.MaxHP} HP remaining).", context);

        if (target.CurrentHP <= 0)
        {
            var ds = GetDeathSaveState(target);
            if (ds.Successes == 0 && ds.Failures == 0)
            {
                target.DeathSaveState = JsonDocument.Parse(JsonSerializer.Serialize(new DeathSaveState { Successes = 0, Failures = 0 })).RootElement;
            }
        }

        context.Combats.Update(combat);
        await context.SaveChangesAsync();
    }

    private static DeathSaveState GetDeathSaveState(CombatParticipant participant)
    {
        if (participant.DeathSaveState.HasValue &&
            participant.DeathSaveState.Value.ValueKind == JsonValueKind.Object)
        {
            return JsonSerializer.Deserialize<DeathSaveState>(participant.DeathSaveState.ToString())
                ?? new DeathSaveState();
        }
        return new DeathSaveState();
    }

    private static async Task AddCombatEventAsync(Combat combat, int round, int turnIndex, CombatEventType type,
        string actorName, string targetName, string content, AppDbContext context)
    {
        var evt = new CombatEvent
        {
            CombatId = combat.Id,
            Round = round,
            TurnIndex = turnIndex,
            Type = type,
            ActorName = actorName,
            TargetName = targetName,
            Content = content
        };
        context.CombatEvents.Add(evt);
        await context.SaveChangesAsync();
    }
}

/// <summary>
/// Factory implementation for combat actions.
/// </summary>
public class CombatActionFactory : ICombatActionFactory
{
    private readonly Dictionary<CombatActionType, ICombatAction> _actions = new();

    public CombatActionFactory()
    {
        RegisterAction(CombatActionType.Attack, new AttackAction());
        RegisterAction(CombatActionType.SaveThrow, new SaveThrowAction());
        RegisterAction(CombatActionType.Spell, new SpellAction());
    }

    public ICombatAction GetAction(CombatActionType actionType)
    {
        return _actions[actionType];
    }

    public void RegisterAction(CombatActionType actionType, ICombatAction action)
    {
        _actions[actionType] = action;
    }
}
