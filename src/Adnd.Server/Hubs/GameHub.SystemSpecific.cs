using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Adnd.Server.Events;
using MediatR;
using System.Text.Json;

namespace Adnd.Server.Hubs;

public partial class GameHub
{
    // ==================== System-Specific ====================

    public async Task<CombatLogResponse> CombatApplySystemEffects(Guid combatId, string systemId, Guid participantId)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var result = await _combatService.ApplySystemSpecificEffectsAsync(combatId, systemId, participantId);
        return BuildCombatLog(result);
    }

    public async Task<CombatLogResponse> CombatCalculateProficiency(Guid combatId, string systemId, int level)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var result = await _combatService.CalculateProficiencyBonusAsync(combatId, systemId, level);
        return BuildCombatLog(result);
    }

    public async Task<CombatLogResponse> CombatCalculateSave(Guid combatId, string systemId, string saveType,
        Guid participantId, int? proficiencyBonus = null)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var result = await _combatService.CalculateSavingThrowAsync(combatId, systemId, saveType, participantId, proficiencyBonus);
        return BuildCombatLog(result);
    }

}
