using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Adnd.Server.Events;
using System.Text.Json;

namespace Adnd.Server.Hubs;

public partial class GameHub
{
    // ==================== Character Progression ====================

    public async Task<CombatLogResponse> CombatAddXP(Guid combatId, Guid participantId, int xpAmount, string reason)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var result = await _combatService.AddXPAsync(combatId, participantId, xpAmount, reason);
        await Clients.Group(combat.GameId.ToString()).SendAsync("CombatXP", new
        {
            participantId,
            xpAmount,
            reason
        });
        return BuildCombatLog(result);
    }

    public async Task<CombatLevelUpResponse> CombatLevelUp(Guid combatId, Guid participantId, int newLevel, string systemId)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var result = await _combatService.LevelUpAsync(combatId, participantId, newLevel, systemId);

        await Clients.Group(combat.GameId.ToString()).SendAsync("CombatLevelUp", new
        {
            participantId,
            newLevel,
            systemId
        });

        return new CombatLevelUpResponse
        {
            ParticipantId = participantId,
            NewLevel = newLevel,
            SystemId = systemId
        };
    }

    public async Task<CombatLogResponse> CombatCalculateXP(Guid combatId, string systemId)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var result = await _combatService.CalculateXPForCombatAsync(combatId, systemId);
        return BuildCombatLog(result);
    }

}
