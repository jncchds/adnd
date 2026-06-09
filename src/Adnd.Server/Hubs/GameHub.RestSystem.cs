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
    // ==================== Rest System ====================

    public async Task<CombatLogResponse> CombatStartShortRest(Guid combatId)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var result = await _combatService.StartShortRestAsync(combatId);
        await Clients.Group(combat.GameId.ToString()).SendAsync("CombatRestStarted", new
        {
            combatId,
            restType = "Short"
        });
        return BuildCombatLog(result);
    }

    public async Task<CombatLogResponse> CombatStartLongRest(Guid combatId)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var result = await _combatService.StartLongRestAsync(combatId);
        await Clients.Group(combat.GameId.ToString()).SendAsync("CombatRestStarted", new
        {
            combatId,
            restType = "Long"
        });
        return BuildCombatLog(result);
    }

    public async Task<CombatLogResponse> CombatEndRest(Guid combatId)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var result = await _combatService.EndRestAsync(combatId);
        await Clients.Group(combat.GameId.ToString()).SendAsync("CombatRestEnded", new { combatId });
        return BuildCombatLog(result);
    }

    public async Task<CombatRestStatusResponse> CombatGetRestStatus(Guid combatId)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var result = await _combatService.GetCurrentRestStatusAsync(combatId);
        return new CombatRestStatusResponse
        {
            RestType = result.RestType,
            IsInProgress = result.IsInProgress,
            RoundsRemaining = result.RoundsRemaining,
            HPRecovered = result.HPRecovered,
            Effects = result.Effects
        };
    }

}
