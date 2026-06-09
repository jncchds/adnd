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
    // ==================== Grid/Map ====================

    public async Task<CombatLogResponse> CombatSetGridSize(Guid combatId, int width, int height)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var result = await _combatService.SetGridSizeAsync(combatId, width, height);
        await Clients.Group(combat.GameId.ToString()).SendAsync("CombatGridSet", new
        {
            combatId,
            width,
            height
        });
        return BuildCombatLog(result);
    }

    public async Task<CombatLogResponse> CombatSetPosition(Guid combatId, Guid participantId, int gridX, int gridY)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var result = await _combatService.SetParticipantPositionAsync(combatId, participantId, gridX, gridY);
        await Clients.Group(combat.GameId.ToString()).SendAsync("CombatPositionSet", new
        {
            participantId,
            gridX,
            gridY
        });
        return BuildCombatLog(result);
    }

    public async Task<CombatLogResponse> CombatMoveParticipant(Guid combatId, Guid participantId, int newGridX, int newGridY)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var result = await _combatService.MoveParticipantAsync(combatId, participantId, newGridX, newGridY);
        await Clients.Group(combat.GameId.ToString()).SendAsync("CombatMove", new
        {
            participantId,
            newGridX,
            newGridY
        });
        return BuildCombatLog(result);
    }

    public async Task<CombatGridPositionResponse?> CombatGetPosition(Guid combatId, Guid participantId)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var result = await _combatService.GetParticipantPositionAsync(combatId, participantId);
        return result == null ? null : new CombatGridPositionResponse
        {
            ParticipantId = result.ParticipantId,
            GridX = result.GridX,
            GridY = result.GridY,
            DisplayName = result.DisplayName,
            MoveSpeed = result.MoveSpeed
        };
    }

    public async Task<List<CombatGridPositionResponse>> CombatGetAdjacentPositions(Guid combatId, int gridX, int gridY, int range = 1)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var positions = await _combatService.GetAdjacentPositionsAsync(combatId, gridX, gridY, range);
        return positions.Select(p => new CombatGridPositionResponse
        {
            GridX = p.GridX,
            GridY = p.GridY
        }).ToList();
    }

}
