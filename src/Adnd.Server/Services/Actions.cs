using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Adnd.Server.Data;
using Adnd.Server.Models;

namespace Adnd.Server.Services;

public partial class CombatService
{
    // ==================== Combat Actions ====================

    public async Task<AttackWithCombatResult> ExecuteAttackAsync(Guid combatId, string attackerName, string weapon,
        Guid targetId, string attackFormula, int? attackBonus = null, string? damageFormula = null,
        int? damageBonus = null, string? description = null)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var target = combat.Participants.FirstOrDefault(p => p.Id == targetId)
            ?? throw new KeyNotFoundException($"Target participant {targetId} not found in combat.");

        // Attack roll
        var attackResult = _diceEngine.Roll(attackFormula);
        var totalAttack = attackResult.Total + (attackBonus ?? 0);
        var hit = totalAttack >= target.AC;

        // Check for critical (natural 20)
        var isCrit = attackResult.Rolls.Length == 1 && attackResult.Rolls[0] == 20;

        // Check for fumble (natural 1)
        var isFumble = attackResult.Rolls.Length == 1 && attackResult.Rolls[0] == 1;

        string damageInfo = string.Empty;
        int damageTotal = 0;

        if (hit)
        {
            if (isCrit)
            {
                // Critical: roll damage dice twice
                if (damageFormula != null)
                {
                    var critDamage = _diceEngine.Roll(damageFormula);
                    var doubledDice = critDamage.Rolls.Concat(critDamage.Rolls).ToArray();
                    damageTotal = doubledDice.Sum() + (damageBonus ?? 0);
                    damageInfo = $"CRITICAL! {damageFormula} doubled + {damageBonus ?? 0} = {damageTotal}";
                }
            }
            else if (damageFormula != null)
            {
                var dmgResult = _diceEngine.Roll(damageFormula);
                damageTotal = dmgResult.Total + (damageBonus ?? 0);
                damageInfo = $"{damageFormula} + {damageBonus ?? 0} = {damageTotal}";

                // Apply damage
                await DealDamageToParticipant(combat, target, damageTotal, attackerName);
            }
        }
        else if (isFumble)
        {
            damageInfo = "Fumble! Weapon malfunctioned.";
            await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
                CombatEventType.Condition, attackerName, attackerName, "Fumbled attack.");
        }

        var result = new AttackWithCombatResult
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

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.Attack, attackerName, target.DisplayName,
            $"{weapon}: {attackFormula}+{attackBonus ?? 0}={totalAttack} vs AC {target.AC} -> {(hit ? "HIT" : "MISS")}"
            + (hit ? $" | {damageInfo}" : ""));

        return result;
    }

    public async Task<SaveThrowResult> ExecuteSaveThrowAsync(Guid combatId, string participantName, Guid participantId,
        string saveType, string saveFormula, int dc)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant {participantId} not found in combat.");

        var rollResult = _diceEngine.Roll(saveFormula);
        var total = rollResult.Total;
        var success = total >= dc;

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.SaveThrow, participantName, participantName,
            $"{saveType}: {saveFormula}={total} vs DC {dc} -> {(success ? "SUCCESS" : "FAILURE")}");

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

}
