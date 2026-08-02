using System.Text.Json;
using Adnd.Server.Events;
using Adnd.Server.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Hubs;

public partial class GameHub
{
    // Combat is run by the Game Master. Every method below is creator-only and scopes the
    // combat (and its participants) to the game — previously none of them checked anything,
    // so any authenticated user could DealDamage(victimGame, anyCombat, anyTarget, 9999).

    public async Task StartCombat(Guid gameId, string name)
    {
        await RequireCreatorAsync(gameId);
        var session = await ResolveGameSessionAsync(gameId);
        var combat = new Combat
        {
            GameId = gameId,
            SessionId = session.Id,
            Name = name,
            Status = CombatStatus.Active,
            CurrentRound = 1
        };
        db.Combats.Add(combat);
        await db.SaveChangesAsync();
        await BroadcastToGameAsync(gameId, "CombatStarted", BuildCombatDto(combat));
        await PublishAsync(new CombatStarted(gameId, combat.Id, name));
    }

    public async Task EndCombat(Guid gameId, Guid combatId)
    {
        await RequireCreatorAsync(gameId);
        var combat = await RequireCombatInGameAsync(gameId, combatId);
        combat.Status = CombatStatus.Finished;
        await db.SaveChangesAsync();
        await BroadcastToGameAsync(gameId, "CombatEnded", BuildCombatDto(combat));
        await PublishAsync(new CombatEnded(gameId, combatId, []));
    }

    public async Task AddParticipant(Guid gameId, Guid combatId, string displayName, int hp, int ac, float initiative = 0)
    {
        await RequireCreatorAsync(gameId);
        await RequireCombatInGameAsync(gameId, combatId);

        var participant = new CombatParticipant
        {
            CombatId = combatId,
            DisplayName = displayName,
            HP = hp, MaxHP = hp, AC = ac, Initiative = initiative,
            ParticipantType = CombatParticipantType.Neutral
        };
        db.CombatParticipants.Add(participant);
        await db.SaveChangesAsync();
        await BroadcastToGameAsync(gameId, "ParticipantAdded", BuildParticipantDto(participant));
        await PublishAsync(new ParticipantAdded(gameId, combatId, participant.Id, displayName));
    }

    public async Task RemoveParticipant(Guid gameId, Guid combatId, Guid participantId)
    {
        await RequireCreatorAsync(gameId);
        var combat = await RequireCombatInGameAsync(gameId, combatId);

        var p = await RequireParticipantAsync(combatId, participantId);
        db.CombatParticipants.Remove(p);
        await db.SaveChangesAsync();

        // Removing a participant shifts the initiative order; clamp so the "current" turn
        // doesn't silently jump to a different combatant.
        await ReindexTurnAsync(combat);

        await BroadcastToGameAsync(gameId, "ParticipantRemoved", new { combatId, participantId });
        await PublishAsync(new ParticipantRemoved(gameId, combatId, participantId));
    }

    public async Task RollInitiative(Guid gameId, Guid combatId, Guid participantId)
    {
        await RequireCreatorAsync(gameId);
        await RequireCombatInGameAsync(gameId, combatId);

        var p = await RequireParticipantAsync(combatId, participantId);
        var roll = diceEngine.Roll("1d20");
        p.Initiative = roll.Total;
        await db.SaveChangesAsync();

        await BroadcastToGameAsync(gameId, "InitiativeRolled", new { combatId, participantId, initiative = roll.Total });
        await PublishAsync(new InitiativeRolled(gameId, combatId, participantId, roll.Total));
    }

    public async Task RollInitiativeForAll(Guid gameId, Guid combatId)
    {
        await RequireCreatorAsync(gameId);
        var combat = await RequireCombatInGameAsync(gameId, combatId);

        foreach (var p in combat.Participants)
            p.Initiative = diceEngine.Roll("1d20").Total;

        // Re-rolling reorders everyone, so the turn pointer has to restart.
        combat.CurrentTurnIndex = 0;
        await db.SaveChangesAsync();

        await BroadcastToGameAsync(gameId, "TurnAdvanced", BuildCombatDto(combat));
        await PublishAsync(new InitiativeRolledForAll(gameId, combatId));
    }

    public async Task NextTurn(Guid gameId, Guid combatId)
    {
        await RequireCreatorAsync(gameId);
        var combat = await RequireCombatInGameAsync(gameId, combatId);

        var count = Math.Max(combat.Participants.Count, 1);
        combat.CurrentTurnIndex = (combat.CurrentTurnIndex + 1) % count;
        if (combat.CurrentTurnIndex == 0) combat.CurrentRound++;
        await db.SaveChangesAsync();

        await BroadcastToGameAsync(gameId, "TurnAdvanced", BuildCombatDto(combat));
        await PublishAsync(new TurnAdvanced(gameId, combatId, combat.CurrentRound, combat.CurrentTurnIndex));
    }

    public async Task PreviousTurn(Guid gameId, Guid combatId)
    {
        await RequireCreatorAsync(gameId);
        var combat = await RequireCombatInGameAsync(gameId, combatId);

        var count = Math.Max(combat.Participants.Count, 1);

        // Stepping back from the first combatant wraps to the end of the previous round.
        // This used to clamp at 0 and never decrement the round, so the round counter
        // drifted permanently out of step after any rewind.
        if (combat.CurrentTurnIndex == 0)
        {
            if (combat.CurrentRound > 1)
            {
                combat.CurrentRound--;
                combat.CurrentTurnIndex = count - 1;
            }
        }
        else
        {
            combat.CurrentTurnIndex--;
        }

        await db.SaveChangesAsync();
        await BroadcastToGameAsync(gameId, "TurnAdvanced", BuildCombatDto(combat));
        await PublishAsync(new TurnRetreated(gameId, combatId));
    }

    public async Task DealDamage(Guid gameId, Guid combatId, Guid targetId, int amount, string damageType = "Physical")
    {
        await RequireCreatorAsync(gameId);
        await RequireCombatInGameAsync(gameId, combatId);
        if (amount < 0) throw new HubException("Damage cannot be negative.");

        var p = await RequireParticipantAsync(combatId, targetId);
        p.HP = Math.Max(0, p.HP - amount);

        // Taking damage while down restarts the death-save clock.
        if (p.HP == 0) p.DeathSaveState = default;

        await db.SaveChangesAsync();
        await BroadcastToGameAsync(gameId, "CombatDamageDealt", new DamageDto(combatId, targetId, amount, damageType, p.HP));
        await PublishAsync(new CombatDamageDealt(gameId, combatId, targetId, amount, damageType));
    }

    public async Task HealParticipant(Guid gameId, Guid combatId, Guid targetId, int amount)
    {
        await RequireCreatorAsync(gameId);
        await RequireCombatInGameAsync(gameId, combatId);
        if (amount < 0) throw new HubException("Healing cannot be negative.");

        var p = await RequireParticipantAsync(combatId, targetId);
        p.HP = Math.Min(p.MaxHP, p.HP + amount);
        if (p.HP > 0) p.DeathSaveState = default;

        await db.SaveChangesAsync();
        await BroadcastToGameAsync(gameId, "CombatHealed", new { combatId, targetId, amount, newHP = p.HP });
        await PublishAsync(new CombatHealed(gameId, combatId, targetId, amount));
    }

    public async Task ApplyCondition(Guid gameId, Guid combatId, Guid participantId, string condition)
    {
        await RequireCreatorAsync(gameId);
        await RequireCombatInGameAsync(gameId, combatId);

        // This used to broadcast without persisting anything, so conditions vanished on reload.
        var p = await RequireParticipantAsync(combatId, participantId);
        var conditions = ReadConditions(p.Conditions).ToList();
        if (!conditions.Contains(condition))
        {
            conditions.Add(condition);
            p.Conditions = JsonSerializer.SerializeToElement(conditions);
            await db.SaveChangesAsync();
        }

        await BroadcastToGameAsync(gameId, "CombatConditionApplied", new ConditionDto(combatId, participantId, condition, true));
        await PublishAsync(new CombatConditionApplied(gameId, combatId, participantId, condition));
    }

    public async Task RemoveCondition(Guid gameId, Guid combatId, Guid participantId, string condition)
    {
        await RequireCreatorAsync(gameId);
        await RequireCombatInGameAsync(gameId, combatId);

        var p = await RequireParticipantAsync(combatId, participantId);
        var conditions = ReadConditions(p.Conditions).Where(c => c != condition).ToList();
        p.Conditions = JsonSerializer.SerializeToElement(conditions);
        await db.SaveChangesAsync();

        await BroadcastToGameAsync(gameId, "CombatConditionRemoved", new ConditionDto(combatId, participantId, condition, false));
        await PublishAsync(new CombatConditionRemoved(gameId, combatId, participantId, condition));
    }

    public async Task RecordDeathSave(Guid gameId, Guid combatId, Guid participantId, bool success, int roll)
    {
        await RequireCreatorAsync(gameId);
        await RequireCombatInGameAsync(gameId, combatId);
        await RequireParticipantAsync(combatId, participantId);

        await BroadcastToGameAsync(gameId, "DeathSaveRecorded", new { combatId, participantId, success, roll });
    }

    private async Task<CombatParticipant> RequireParticipantAsync(Guid combatId, Guid participantId)
        => await db.CombatParticipants.FirstOrDefaultAsync(p => p.Id == participantId && p.CombatId == combatId)
           ?? throw new HubForbiddenException("Participant does not belong to this combat.");

    private async Task ReindexTurnAsync(Combat combat)
    {
        var remaining = await db.CombatParticipants.CountAsync(p => p.CombatId == combat.Id);
        if (remaining == 0)
        {
            combat.CurrentTurnIndex = 0;
        }
        else if (combat.CurrentTurnIndex >= remaining)
        {
            combat.CurrentTurnIndex = remaining - 1;
        }
        await db.SaveChangesAsync();
    }

    private static CombatDto BuildCombatDto(Combat combat)
    {
        var parts = combat.Participants
            .OrderByDescending(p => p.Initiative)
            .Select(BuildParticipantDto)
            .ToList();
        return new CombatDto(combat.Id, combat.GameId, combat.SessionId, combat.Name, combat.Status.ToString(), combat.CurrentRound, combat.CurrentTurnIndex, parts);
    }

    private static ParticipantDto BuildParticipantDto(CombatParticipant p) => new(
        p.Id, p.DisplayName, p.Initiative, p.HP, p.MaxHP, p.AC, p.ParticipantType.ToString(),
        ReadConditions(p.Conditions),
        p.ActionsRemaining, p.BonusActionsRemaining, p.ReactionsRemaining, p.MovementsRemaining,
        ReadDeathSaveState(p.DeathSaveState));

    private static IReadOnlyList<string> ReadConditions(JsonElement conditions)
    {
        if (conditions.ValueKind != JsonValueKind.Array) return [];
        return conditions.EnumerateArray()
            .Where(e => e.ValueKind == JsonValueKind.String)
            .Select(e => e.GetString()!)
            .ToList();
    }

    private static DeathSaveStateDto? ReadDeathSaveState(JsonElement state)
    {
        if (state.ValueKind != JsonValueKind.Object) return null;
        return new DeathSaveStateDto(
            state.TryGetProperty("successes", out var s) && s.TryGetInt32(out var sv) ? sv : 0,
            state.TryGetProperty("failures", out var f) && f.TryGetInt32(out var fv) ? fv : 0,
            state.TryGetProperty("isDead", out var d) && d.ValueKind == JsonValueKind.True,
            state.TryGetProperty("isStable", out var st) && st.ValueKind == JsonValueKind.True);
    }
}
