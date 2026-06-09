using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Adnd.Server.Data;
using Adnd.Server.Models;

namespace Adnd.Server.Services;

public partial class CombatService
{
    // ==================== Query Methods ====================

    public async Task<CombatLog> GetCombatLogAsync(Guid combatId)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var events = await _context.CombatEvents
            .Where(e => e.CombatId == combatId)
            .OrderBy(e => e.CreatedAt)
            .ToListAsync();

        return new CombatLog
        {
            CombatId = combatId,
            Name = combat.Name,
            Status = combat.Status,
            CurrentRound = combat.CurrentRound,
            CurrentTurnIndex = combat.CurrentTurnIndex,
            Participants = combat.Participants
                .OrderBy(p => p.Initiative)
                .ThenByDescending(p => p.InitiativeCount)
                .Select(p => new ParticipantSummary
                {
                    Id = p.Id,
                    DisplayName = p.DisplayName,
                    ParticipantType = p.ParticipantType,
                    CurrentHP = p.CurrentHP,
                    MaxHP = p.MaxHP,
                    AC = p.AC,
                    Initiative = p.Initiative,
                    Conditions = JsonSerializer.Deserialize<List<ConditionEntry>>(p.Conditions.ToString()) ?? new(),
                    IsCurrentTurn = p.Id == combat.Participants[Math.Min(combat.CurrentTurnIndex, combat.Participants.Count - 1)].Id,
                    IsDead = p.CurrentHP <= 0
                })
                .ToList(),
            Events = events.Select(e => new CombatLogEvent
            {
                Id = e.Id,
                Round = e.Round,
                TurnIndex = e.TurnIndex,
                Type = e.Type,
                ActorName = e.ActorName,
                TargetName = e.TargetName,
                Content = e.Content,
                Metadata = e.Metadata,
                CreatedAt = e.CreatedAt
            }).ToList()
        };
    }

    public async Task<Combat?> GetCombatAsync(Guid combatId)
    {
        return await _context.Combats
            .Include(c => c.Participants)
            .Include(c => c.Events)
            .FirstOrDefaultAsync(c => c.Id == combatId);
    }

    public async Task<List<Combat>> GetActiveCombatAsync(Guid gameId)
    {
        return await _context.Combats
            .Include(c => c.Participants)
            .Where(c => c.GameId == gameId && c.Status == CombatStatus.Active)
            .OrderByDescending(c => c.StartedAt)
            .ToListAsync();
    }

}
