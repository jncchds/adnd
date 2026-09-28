using Adnd.Server.Models;
using Adnd.Server.Services;
using Microsoft.AspNetCore.SignalR;

namespace Adnd.Server.Hubs;

public partial class GameHub
{
    // The creator's manual combat controls. Every method is creator-only and scopes the combat
    // (and its participants) to the game — previously none of them checked anything, so any
    // authenticated user could DealDamage(victimGame, anyCombat, anyTarget, 9999).
    //
    // State changes go through ICombatTracker, the same path the AI GM's combat tools take,
    // and it pushes the whole combat as CombatUpdated after each one. The per-change events
    // this used to send (ParticipantAdded, InitiativeRolled, DeathSaveRecorded, …) had no
    // listener on the client, so the panel silently went stale after most of them.

    public async Task StartCombat(Guid gameId, string name, string[]? zones = null)
    {
        await RequireCreatorAsync(gameId);
        var session = await ResolveGameSessionAsync(gameId);
        var specs = (zones ?? []).Select(z => new ZoneSpec(z, [])).ToList();
        await Rule(() => combats.StartAsync(gameId, session.Id, name, specs, null));
    }

    public async Task EndCombat(Guid gameId, Guid combatId)
    {
        await RequireCreatorAsync(gameId);
        var combat = await RequireActiveCombatAsync(gameId, combatId);
        await combats.EndAsync(combat, null);
    }

    public async Task AddParticipant(Guid gameId, Guid combatId, string displayName, int hp, int ac, string? zone = null, float? initiative = null)
    {
        await RequireCreatorAsync(gameId);
        var combat = await RequireActiveCombatAsync(gameId, combatId);
        await Rule(() => combats.AddParticipantAsync(combat, displayName, hp, ac, zone, initiative));
    }

    public async Task RemoveParticipant(Guid gameId, Guid combatId, Guid participantId)
    {
        await RequireCreatorAsync(gameId);
        var combat = await RequireActiveCombatAsync(gameId, combatId);
        await Rule(() => combats.RemoveParticipantAsync(combat, participantId));
    }

    public async Task MoveParticipant(Guid gameId, Guid combatId, string participantName, string zone)
    {
        await RequireCreatorAsync(gameId);
        var combat = await RequireActiveCombatAsync(gameId, combatId);
        await Rule(() => combats.MoveAsync(combat, participantName, zone));
    }

    public async Task RollInitiative(Guid gameId, Guid combatId, Guid participantId)
    {
        await RequireCreatorAsync(gameId);
        var combat = await RequireActiveCombatAsync(gameId, combatId);
        await Rule(() => combats.RollInitiativeAsync(combat, participantId));
    }

    public async Task RollInitiativeForAll(Guid gameId, Guid combatId)
    {
        await RequireCreatorAsync(gameId);
        var combat = await RequireActiveCombatAsync(gameId, combatId);
        await combats.RollInitiativeAsync(combat, null);
    }

    public async Task NextTurn(Guid gameId, Guid combatId)
    {
        await RequireCreatorAsync(gameId);
        var combat = await RequireActiveCombatAsync(gameId, combatId);
        await combats.AdvanceTurnAsync(combat);
    }

    public async Task PreviousTurn(Guid gameId, Guid combatId)
    {
        await RequireCreatorAsync(gameId);
        var combat = await RequireActiveCombatAsync(gameId, combatId);
        await combats.AdvanceTurnAsync(combat, backwards: true);
    }

    public async Task DealDamage(Guid gameId, Guid combatId, Guid targetId, int amount)
    {
        await RequireCreatorAsync(gameId);
        var combat = await RequireActiveCombatAsync(gameId, combatId);
        if (amount < 0) throw new HubException("Damage cannot be negative.");
        await Rule(() => combats.ChangeHpAsync(combat, targetId, -amount));
    }

    public async Task HealParticipant(Guid gameId, Guid combatId, Guid targetId, int amount)
    {
        await RequireCreatorAsync(gameId);
        var combat = await RequireActiveCombatAsync(gameId, combatId);
        if (amount < 0) throw new HubException("Healing cannot be negative.");
        await Rule(() => combats.ChangeHpAsync(combat, targetId, amount));
    }

    public async Task ApplyCondition(Guid gameId, Guid combatId, Guid participantId, string condition)
    {
        await RequireCreatorAsync(gameId);
        var combat = await RequireActiveCombatAsync(gameId, combatId);
        await Rule(() => combats.SetConditionAsync(combat, participantId, condition, applied: true));
    }

    public async Task RemoveCondition(Guid gameId, Guid combatId, Guid participantId, string condition)
    {
        await RequireCreatorAsync(gameId);
        var combat = await RequireActiveCombatAsync(gameId, combatId);
        await Rule(() => combats.SetConditionAsync(combat, participantId, condition, applied: false));
    }

    /// <summary>The roll is the d20 result; success, failure, a natural 1 and a natural 20
    /// follow from it. This used to broadcast the save and record nothing.</summary>
    public async Task RecordDeathSave(Guid gameId, Guid combatId, Guid participantId, int roll)
    {
        await RequireCreatorAsync(gameId);
        var combat = await RequireActiveCombatAsync(gameId, combatId);
        await Rule(() => combats.RecordDeathSaveAsync(combat, participantId, roll));
    }

    /// <summary>Confirms the combat belongs to the game and is still being fought.</summary>
    private async Task<Combat> RequireActiveCombatAsync(Guid gameId, Guid combatId)
    {
        var combat = await RequireCombatInGameAsync(gameId, combatId);
        return combat.Status == CombatStatus.Finished
            ? throw new HubException("That combat is already over.")
            : combat;
    }

    /// <summary>A broken combat rule reaches the caller as a readable message, not a fault.</summary>
    private static async Task Rule(Func<Task> action)
    {
        try { await action(); }
        catch (CombatRuleException ex) { throw new HubException(ex.Message); }
    }
}
