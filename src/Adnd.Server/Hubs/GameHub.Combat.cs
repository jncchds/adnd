using Adnd.Server.Events;
using Adnd.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Hubs;

public partial class GameHub
{
    public async Task StartCombat(Guid gameId, string name)
    {
        var session = await ResolveGameSessionAsync(gameId);
        var combat = new Combat { GameId = gameId, SessionId = session.Id, Name = name };
        db.Combats.Add(combat);
        await db.SaveChangesAsync();
        await BroadcastToGameAsync(gameId, "CombatStarted", BuildCombatDto(combat));
        await PublishAsync(new CombatStarted(gameId, combat.Id, name));
    }

    public async Task EndCombat(Guid gameId, Guid combatId)
    {
        var combat = await db.Combats.Include(c => c.Participants).FirstOrDefaultAsync(c => c.Id == combatId);
        if (combat == null) return;
        combat.Status = CombatStatus.Finished;
        await db.SaveChangesAsync();
        await BroadcastToGameAsync(gameId, "CombatEnded", BuildCombatDto(combat));
        await PublishAsync(new CombatEnded(gameId, combatId, []));
    }

    public async Task AddParticipant(Guid gameId, Guid combatId, string displayName, int hp, int ac, float initiative = 0)
    {
        var participant = new CombatParticipant
        {
            CombatId = combatId,
            DisplayName = displayName,
            HP = hp, MaxHP = hp, AC = ac, Initiative = initiative,
            ParticipantType = CombatParticipantType.Neutral
        };
        db.CombatParticipants.Add(participant);
        await db.SaveChangesAsync();
        await BroadcastToGameAsync(gameId, "ParticipantAdded",
            new ParticipantDto(participant.Id, displayName, initiative, hp, hp, ac, "Neutral"));
        await PublishAsync(new ParticipantAdded(gameId, combatId, participant.Id, displayName));
    }

    public async Task RemoveParticipant(Guid gameId, Guid combatId, Guid participantId)
    {
        var p = await db.CombatParticipants.FindAsync(participantId);
        if (p != null) { db.CombatParticipants.Remove(p); await db.SaveChangesAsync(); }
        await BroadcastToGameAsync(gameId, "ParticipantRemoved", new { combatId, participantId });
        await PublishAsync(new ParticipantRemoved(gameId, combatId, participantId));
    }

    public async Task RollInitiative(Guid gameId, Guid combatId, Guid participantId)
    {
        var roll = diceEngine.Roll("1d20");
        var p = await db.CombatParticipants.FindAsync(participantId);
        if (p != null) { p.Initiative = roll.Total; await db.SaveChangesAsync(); }
        await BroadcastToGameAsync(gameId, "InitiativeRolled", new { combatId, participantId, initiative = roll.Total });
        await PublishAsync(new InitiativeRolled(gameId, combatId, participantId, roll.Total));
    }

    public async Task RollInitiativeForAll(Guid gameId, Guid combatId)
    {
        var participants = await db.CombatParticipants.Where(p => p.CombatId == combatId).ToListAsync();
        foreach (var p in participants)
            p.Initiative = diceEngine.Roll("1d20").Total;
        await db.SaveChangesAsync();
        var combat = await db.Combats.Include(c => c.Participants).FirstOrDefaultAsync(c => c.Id == combatId);
        if (combat != null) await BroadcastToGameAsync(gameId, "TurnAdvanced", BuildCombatDto(combat));
        await PublishAsync(new InitiativeRolledForAll(gameId, combatId));
    }

    public async Task NextTurn(Guid gameId, Guid combatId)
    {
        var combat = await db.Combats.Include(c => c.Participants).FirstOrDefaultAsync(c => c.Id == combatId);
        if (combat == null) return;
        var count = Math.Max(combat.Participants.Count, 1);
        combat.CurrentTurnIndex = (combat.CurrentTurnIndex + 1) % count;
        if (combat.CurrentTurnIndex == 0) combat.CurrentRound++;
        await db.SaveChangesAsync();
        await BroadcastToGameAsync(gameId, "TurnAdvanced", BuildCombatDto(combat));
        await PublishAsync(new TurnAdvanced(gameId, combatId, combat.CurrentRound, combat.CurrentTurnIndex));
    }

    public async Task PreviousTurn(Guid gameId, Guid combatId)
    {
        var combat = await db.Combats.Include(c => c.Participants).FirstOrDefaultAsync(c => c.Id == combatId);
        if (combat == null) return;
        combat.CurrentTurnIndex = Math.Max(0, combat.CurrentTurnIndex - 1);
        await db.SaveChangesAsync();
        await BroadcastToGameAsync(gameId, "TurnAdvanced", BuildCombatDto(combat));
        await PublishAsync(new TurnRetreated(gameId, combatId));
    }

    public async Task DealDamage(Guid gameId, Guid combatId, Guid targetId, int amount, string damageType = "Physical")
    {
        var p = await db.CombatParticipants.FindAsync(targetId);
        if (p == null) return;
        p.HP = Math.Max(0, p.HP - amount);
        await db.SaveChangesAsync();
        await BroadcastToGameAsync(gameId, "CombatDamageDealt", new DamageDto(combatId, targetId, amount, damageType, p.HP));
        await PublishAsync(new CombatDamageDealt(gameId, combatId, targetId, amount, damageType));
    }

    public async Task HealParticipant(Guid gameId, Guid combatId, Guid targetId, int amount)
    {
        var p = await db.CombatParticipants.FindAsync(targetId);
        if (p == null) return;
        p.HP = Math.Min(p.MaxHP, p.HP + amount);
        await db.SaveChangesAsync();
        await BroadcastToGameAsync(gameId, "CombatHealed", new { combatId, targetId, amount, newHP = p.HP });
        await PublishAsync(new CombatHealed(gameId, combatId, targetId, amount));
    }

    public async Task ApplyCondition(Guid gameId, Guid combatId, Guid participantId, string condition)
    {
        await BroadcastToGameAsync(gameId, "CombatConditionApplied", new ConditionDto(combatId, participantId, condition, true));
        await PublishAsync(new CombatConditionApplied(gameId, combatId, participantId, condition));
    }

    public async Task RemoveCondition(Guid gameId, Guid combatId, Guid participantId, string condition)
    {
        await BroadcastToGameAsync(gameId, "CombatConditionRemoved", new ConditionDto(combatId, participantId, condition, false));
        await PublishAsync(new CombatConditionRemoved(gameId, combatId, participantId, condition));
    }

    public async Task RecordDeathSave(Guid gameId, Guid combatId, Guid participantId, bool success, int roll)
    {
        await BroadcastToGameAsync(gameId, "DeathSaveRecorded", new { combatId, participantId, success, roll });
    }

    private static CombatDto BuildCombatDto(Combat combat)
    {
        var parts = combat.Participants
            .OrderByDescending(p => p.Initiative)
            .Select(p => new ParticipantDto(p.Id, p.DisplayName, p.Initiative, p.HP, p.MaxHP, p.AC, p.ParticipantType.ToString()))
            .ToList();
        return new CombatDto(combat.Id, combat.GameId, combat.Name, combat.Status.ToString(), combat.CurrentRound, combat.CurrentTurnIndex, parts);
    }
}
