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

/// <summary>
/// Unified parameters for any combat action. Each action type uses the fields it needs.
/// </summary>
public class CombatActionParams
{
    public string? AttackerName { get; init; }
    public string? Weapon { get; init; }
    public Guid TargetId { get; init; }
    public string? AttackFormula { get; init; }
    public int? AttackBonus { get; init; }
    public string? DamageFormula { get; init; }
    public int? DamageBonus { get; init; }
    public string? Description { get; init; }
    public string? ParticipantName { get; init; }
    public Guid ParticipantId { get; init; }
    public string? SaveType { get; init; }
    public string? SaveFormula { get; init; }
    public int? DC { get; init; }
    public string? CasterName { get; init; }
    public string? SpellName { get; init; }
    public int? SaveDC { get; init; }
}

public interface ICombatAction
{
    string ActionName { get; }
    Task<object> ExecuteAsync(Combat combat, CombatActionParams @params, AppDbContext context, IDiceEngine diceEngine,
        ISystemRules rules, ILogger logger);
}

/// <summary>
/// Concrete action for executing an attack.
/// </summary>
public class AttackAction : ICombatAction
{
    public string ActionName => "Attack";

    public async Task<object> ExecuteAsync(Combat combat, CombatActionParams @params, AppDbContext context, IDiceEngine diceEngine,
        ISystemRules rules, ILogger logger)
    {
        var target = combat.Participants.FirstOrDefault(p => p.Id == @params.TargetId)
            ?? throw new KeyNotFoundException($"Target participant {@params.TargetId} not found in combat.");

        var attackResult = diceEngine.Roll(@params.AttackFormula!);
        var totalAttack = attackResult.Total + (@params.AttackBonus ?? 0);
        var hit = totalAttack >= target.AC;

        var isCrit = rules.IsCriticalHit(attackResult.Rolls.Length == 1 ? attackResult.Rolls[0] : 20, combat.GameId.ToString());
        var isFumble = rules.IsFumble(attackResult.Rolls.Length == 1 ? attackResult.Rolls[0] : 1, combat.GameId.ToString());

        string damageInfo = string.Empty;
        int damageTotal = 0;

        if (hit)
        {
            if (isCrit && @params.DamageFormula != null)
            {
                var critDamage = diceEngine.Roll(@params.DamageFormula);
                damageTotal = critDamage.Rolls.Concat(critDamage.Rolls).Sum() + (@params.DamageBonus ?? 0);
                damageInfo = $"CRITICAL! {@params.DamageFormula} doubled + {@params.DamageBonus ?? 0} = {damageTotal}";
            }
            else if (@params.DamageFormula != null)
            {
                var dmgResult = diceEngine.Roll(@params.DamageFormula);
                damageTotal = dmgResult.Total + (@params.DamageBonus ?? 0);
                damageInfo = $"{@params.DamageFormula} + {@params.DamageBonus ?? 0} = {damageTotal}";
                await HandleDamageAsync(combat, target, damageTotal, @params.AttackerName!, context);
            }
        }
        else if (isFumble)
        {
            damageInfo = "Fumble! Weapon malfunctioned.";
            await AddCombatEventAsync(combat, combat.CurrentRound, combat.CurrentTurnIndex,
                CombatEventType.Condition, @params.AttackerName!, @params.AttackerName!, "Fumbled attack.", context);
        }

        await AddCombatEventAsync(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.Attack, @params.AttackerName!, target.DisplayName,
            $"{@params.Weapon}: {@params.AttackFormula}+{@params.AttackBonus ?? 0}={totalAttack} vs AC {target.AC} -> {(hit ? "HIT" : "MISS")}"
            + (hit ? $" | {damageInfo}" : ""), context);

        return new AttackWithCombatResult
        {
            Attacker = @params.AttackerName!,
            Weapon = @params.Weapon!,
            Target = target.DisplayName,
            Hit = hit,
            IsCritical = isCrit,
            IsFumble = isFumble,
            AttackRoll = totalAttack,
            AttackDice = attackResult.Total,
            AC = target.AC,
            DamageDice = @params.DamageFormula ?? string.Empty,
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

    public async Task<object> ExecuteAsync(Combat combat, CombatActionParams @params, AppDbContext context, IDiceEngine diceEngine,
        ISystemRules rules, ILogger logger)
    {
        var participant = combat.Participants.FirstOrDefault(p => p.Id == @params.ParticipantId)
            ?? throw new KeyNotFoundException($"Participant {@params.ParticipantId} not found in combat.");

        var rollResult = diceEngine.Roll(@params.SaveFormula!);
        var total = rollResult.Total;
        var success = total >= (@params.DC ?? 10);

        await AddCombatEventAsync(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.SaveThrow, @params.ParticipantName!, @params.ParticipantName!,
            $"{@params.SaveType}: {@params.SaveFormula}={total} vs DC {@params.DC ?? 10} -> {(success ? "SUCCESS" : "FAILURE")}", context);

        return new SaveThrowResult
        {
            Participant = participant.DisplayName,
            SaveType = @params.SaveType!,
            DiceRoll = total,
            DC = @params.DC ?? 10,
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

    public async Task<object> ExecuteAsync(Combat combat, CombatActionParams @params, AppDbContext context, IDiceEngine diceEngine,
        ISystemRules rules, ILogger logger)
    {
        var target = combat.Participants.FirstOrDefault(p => p.Id == @params.TargetId)
            ?? throw new KeyNotFoundException($"Target participant {@params.TargetId} not found in combat.");

        var saveResult = diceEngine.Roll(@params.SaveFormula!);
        var saveTotal = saveResult.Total;
        var saveSuccess = saveTotal >= (@params.SaveDC ?? 10);

        var isCritSave = saveResult.Rolls.Length == 1 && saveResult.Rolls[0] == 20;
        if (isCritSave) saveSuccess = true;

        var isFumbleSave = saveResult.Rolls.Length == 1 && saveResult.Rolls[0] == 1;
        if (isFumbleSave) saveSuccess = false;

        int damageTotal = 0;
        string damageInfo = string.Empty;
        string effect = string.Empty;

        if (@params.DamageFormula != null && !saveSuccess)
        {
            var dmgResult = diceEngine.Roll(@params.DamageFormula);
            damageTotal = dmgResult.Total + (@params.DamageBonus ?? 0);
            damageInfo = $"{@params.DamageFormula} + {@params.DamageBonus ?? 0} = {damageTotal}";
            await HandleDamageAsync(combat, target, damageTotal, @params.CasterName!, context);
        }
        else if (@params.DamageFormula != null && saveSuccess)
        {
            var dmgResult = diceEngine.Roll(@params.DamageFormula);
            damageTotal = (dmgResult.Total + (@params.DamageBonus ?? 0)) / 2;
            damageInfo = $"Half: {@params.DamageFormula} + {@params.DamageBonus ?? 0} = {damageTotal} (half)";
            await HandleDamageAsync(combat, target, damageTotal, @params.CasterName!, context);
        }
        else if (@params.Description != null)
        {
            effect = @params.Description;
        }

        await AddCombatEventAsync(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.Healing, @params.CasterName!, target.DisplayName,
            $"Cast {@params.SpellName}: {@params.SaveFormula}={saveTotal} vs DC {@params.SaveDC ?? 10} -> {(saveSuccess ? "SUCCESS" : "FAILURE")}"
            + (damageTotal > 0 ? $" | {damageInfo}" : $" | {effect}"), context);

        return new SpellCastResult
        {
            Caster = @params.CasterName!,
            SpellName = @params.SpellName!,
            Target = target.DisplayName,
            SaveType = @params.SaveFormula!,
            SaveDC = @params.SaveDC ?? 10,
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
