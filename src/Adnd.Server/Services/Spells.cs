using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Adnd.Server.Data;
using Adnd.Server.Models;

namespace Adnd.Server.Services;

public partial class CombatService
{
    // ==================== Spell Combat ====================

    public async Task<SpellCastResult> CastSpellAsync(Guid combatId, string casterName, string spellName,
        Guid targetId, string saveFormula, int saveDC, string? damageFormula = null,
        int? damageBonus = null, string? description = null)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var target = combat.Participants.FirstOrDefault(p => p.Id == targetId)
            ?? throw new KeyNotFoundException($"Target participant {targetId} not found in combat.");

        // Target makes saving throw
        var saveResult = _diceEngine.Roll(saveFormula);
        var saveTotal = saveResult.Total;
        var saveSuccess = saveTotal >= saveDC;

        // Check for critical save (natural 20)
        var isCritSave = saveResult.Rolls.Length == 1 && saveResult.Rolls[0] == 20;
        if (isCritSave) saveSuccess = true; // Auto-success on natural 20

        // Check for fumble save (natural 1)
        var isFumbleSave = saveResult.Rolls.Length == 1 && saveResult.Rolls[0] == 1;
        if (isFumbleSave) saveSuccess = false; // Auto-failure on natural 1

        int damageTotal = 0;
        string damageInfo = string.Empty;
        string effect = string.Empty;

        // Apply spell damage or effect
        if (damageFormula != null && !saveSuccess)
        {
            var dmgResult = _diceEngine.Roll(damageFormula);
            damageTotal = dmgResult.Total + (damageBonus ?? 0);
            damageInfo = $"{damageFormula} + {damageBonus ?? 0} = {damageTotal}";
            await DealDamageToParticipant(combat, target, damageTotal, casterName);
        }
        else if (damageFormula != null && saveSuccess)
        {
            // Half damage on successful save (D&D 5e style)
            var dmgResult = _diceEngine.Roll(damageFormula);
            damageTotal = (dmgResult.Total + (damageBonus ?? 0)) / 2;
            damageInfo = $"Half: {damageFormula} + {damageBonus ?? 0} = {damageTotal} (half)";
            await DealDamageToParticipant(combat, target, damageTotal, casterName);
        }
        else if (description != null)
        {
            effect = description;
        }

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.Healing, casterName, target.DisplayName,
            $"Cast {spellName}: {saveFormula}={saveTotal} vs DC {saveDC} -> {(saveSuccess ? "SUCCESS" : "FAILURE")}"
            + (damageTotal > 0 ? $" | {damageInfo}" : $" | {effect}"));

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

    public async Task<SpellCastResult> CastAreaSpellAsync(Guid combatId, string casterName, string spellName,
        string saveFormula, int saveDC, string? damageFormula = null,
        int? damageBonus = null, string? description = null, Guid[]? targetIds = null)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var targets = targetIds != null
            ? combat.Participants.Where(p => targetIds.Contains(p.Id)).ToList()
            : combat.Participants.Where(p => p.CurrentHP > 0).ToList();

        if (!targets.Any())
            throw new InvalidOperationException("No valid targets for area spell.");

        int totalDamage = 0;
        var results = new List<string>();

        foreach (var target in targets)
        {
            var saveResult = _diceEngine.Roll(saveFormula);
            var saveTotal = saveResult.Total;
            var saveSuccess = saveTotal >= saveDC;

            int dmg = 0;
            if (damageFormula != null && !saveSuccess)
            {
                var dmgResult = _diceEngine.Roll(damageFormula);
                dmg = dmgResult.Total + (damageBonus ?? 0);
                await DealDamageToParticipant(combat, target, dmg, casterName);
                totalDamage += dmg;
            }
            else if (damageFormula != null && saveSuccess)
            {
                var dmgResult = _diceEngine.Roll(damageFormula);
                dmg = (dmgResult.Total + (damageBonus ?? 0)) / 2;
                await DealDamageToParticipant(combat, target, dmg, casterName);
                totalDamage += dmg;
            }

            var dmgStr = dmg > 0 ? $" ({dmg} dmg)" : "";
            results.Add($"{target.DisplayName}: {saveFormula}={saveTotal} vs DC {saveDC} -> {(saveSuccess ? "SUCCESS" : "FAILURE")}{dmgStr}");
        }

        var effect = description ?? $"{targets.Count} target(s) affected, {totalDamage} total damage";

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.Healing, casterName, "Area",
            $"Cast {spellName} (AoE): {string.Join(", ", results)} | {effect}");

        return new SpellCastResult
        {
            Caster = casterName,
            SpellName = spellName,
            Target = $"{targets.Count} targets",
            SaveType = saveFormula,
            SaveDC = saveDC,
            SaveSuccess = true,
            DamageTotal = totalDamage,
            DamageInfo = effect,
            Effect = $"Area of Effect: {targets.Count} targets",
            RolledAt = DateTime.UtcNow
        };
    }

}
