using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Adnd.Server.Data;
using Adnd.Server.Models;

namespace Adnd.Server.Services;

public partial class CombatService
{
    // ==================== Combat Lifecycle ====================

    public async Task<Combat> StartCombatAsync(Guid gameId, Guid? sessionId, string? name = null)
    {
        var combat = new Combat
        {
            GameId = gameId,
            SessionId = sessionId,
            Name = name,
            Status = CombatStatus.Active,
            CurrentRound = 1,
            CurrentTurnIndex = 0,
            Participants = new List<CombatParticipant>(),
            Events = new List<CombatEvent>()
        };

        _context.Combats.Add(combat);
        await _context.SaveChangesAsync();

        await AddCombatEvent(combat, 0, 0, CombatEventType.CombatStart, "System", "System",
            $"Combat '{name}' started.");

        _logger.LogInformation("Combat started: {CombatId} in game {GameId}", combat.Id, gameId);
        return combat;
    }

    public async Task<Combat> EndCombatAsync(Guid combatId, string? result = null)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        combat.Status = CombatStatus.Finished;
        combat.EndedAt = DateTime.UtcNow;

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.CombatEnd, "System", "System",
            result ?? "Combat ended.");

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Combat ended: {CombatId} with result: {Result}", combatId, result ?? "unknown");
        return combat;
    }

    public async Task<Combat> PauseCombatAsync(Guid combatId)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        combat.Status = CombatStatus.Paused;
        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();
        return combat;
    }

    public async Task<Combat> ResumeCombatAsync(Guid combatId)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        combat.Status = CombatStatus.Active;
        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();
        return combat;
    }

}
