using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Adnd.Server.Data;
using Adnd.Server.Models;

namespace Adnd.Server.Services;

public partial class CombatService
{
    // ==================== Helpers ====================

    private async Task<Combat> DealDamageToParticipant(Combat combat, CombatParticipant participant, int damage, string source)
    {
        // Apply damage: first to temporary HP, then to current HP
        var tempHP = participant.TemporaryHP?.GetInt32() ?? 0;
        int damageToHP = damage;

        if (tempHP > 0)
        {
            if (damage <= tempHP)
            {
                participant.TemporaryHP = JsonDocument.Parse(JsonSerializer.Serialize(0)).RootElement;
                await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
                    CombatEventType.Damage, source, participant.DisplayName,
                    $"Took {damage} damage (absorbed by {damage} temp HP).");
                return combat;
            }
            else
            {
                damageToHP = damage - tempHP;
                participant.TemporaryHP = JsonDocument.Parse(JsonSerializer.Serialize(0)).RootElement;
            }
        }

        participant.CurrentHP = Math.Max(0, participant.CurrentHP - damageToHP);

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.Damage, source, participant.DisplayName,
            $"Took {damageToHP} damage ({participant.CurrentHP}/{participant.MaxHP} HP remaining).");

        // Check for death - start death saves if HP is 0 or below
        if (participant.CurrentHP <= 0)
        {
            // Start death saves if not already in them
            var ds = GetDeathSaveState(participant);
            if (ds.Successes == 0 && ds.Failures == 0)
            {
                participant.DeathSaveState = JsonDocument.Parse(JsonSerializer.Serialize(new DeathSaveState { Successes = 0, Failures = 0 })).RootElement;
            }
        }

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();
        return combat;
    }

    private async Task<List<ConditionEntry>> GetConditions(CombatParticipant participant)
    {
        if (participant.Conditions.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            return JsonSerializer.Deserialize<List<ConditionEntry>>(participant.Conditions.ToString())
                ?? new List<ConditionEntry>();
        }
        return new List<ConditionEntry>();
    }

    private DeathSaveState GetDeathSaveState(CombatParticipant participant)
    {
        if (participant.DeathSaveState.HasValue &&
            participant.DeathSaveState.Value.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            var ds = participant.DeathSaveState;
            return JsonSerializer.Deserialize<DeathSaveState>(ds.ToString())
                ?? new DeathSaveState();
        }
        return new DeathSaveState();
    }

    private async Task AddCombatEvent(Combat combat, int round, int turnIndex, CombatEventType type,
        string actorName, string targetName, string content)
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
        _context.CombatEvents.Add(evt);
        await _context.SaveChangesAsync();
    }

    private async Task<Combat?> GetCombatWithParticipants(Guid combatId)
    {
        return await _context.Combats
            .Include(c => c.Participants)
            .Include(c => c.Events)
            .FirstOrDefaultAsync(c => c.Id == combatId);
    }

}
