using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Adnd.Server.Data;
using Adnd.Server.Models;

namespace Adnd.Server.Services;

public partial class CombatService
{
    // ==================== Grid/Map ====================

    public async Task<Combat> SetGridSizeAsync(Guid combatId, int width, int height)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        // Store grid size in combat notes
        var notes = new Dictionary<string, object>
        {
            { "gridWidth", width },
            { "gridHeight", height }
        };
        combat.Notes = JsonDocument.Parse(JsonSerializer.Serialize(notes)).RootElement;

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.RoundStart, "System", "System",
            $"Grid size set to {width}x{height}.");

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();
        return combat;
    }

    public async Task<Combat> SetParticipantPositionAsync(Guid combatId, Guid participantId, int gridX, int gridY)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant {participantId} not found in combat.");

        // Store position in participant notes
        var notes = GetNotes(participant);
        if (notes == null) notes = new Dictionary<string, object>();
        notes["gridX"] = gridX;
        notes["gridY"] = gridY;
        participant.Notes = JsonDocument.Parse(JsonSerializer.Serialize(notes)).RootElement;

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.Condition, "System", participant.DisplayName,
            $"Moved to grid position ({gridX}, {gridY}).");

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();
        return combat;
    }

    public async Task<Combat> MoveParticipantAsync(Guid combatId, Guid participantId, int newGridX, int newGridY)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant {participantId} not found in combat.");

        // Check movement cost
        var notes = GetNotes(participant);
        var moveSpeed = 30; // Default 30 feet
        if (notes != null && notes.ContainsKey("moveSpeed"))
        {
            moveSpeed = Convert.ToInt32(notes["moveSpeed"]);
        }

        // Calculate distance (Manhattan distance for grid)
        var currentX = notes != null && notes.ContainsKey("gridX") ? Convert.ToInt32(notes["gridX"]) : 0;
        var currentY = notes != null && notes.ContainsKey("gridY") ? Convert.ToInt32(notes["gridY"]) : 0;
        var distance = Math.Abs(newGridX - currentX) + Math.Abs(newGridY - currentY);

        // Each 5 feet = 1 grid square
        var squaresMoved = (distance + 4) / 5; // Ceiling division
        var movementCost = squaresMoved * 5;

        if (movementCost > moveSpeed)
        {
            await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
                CombatEventType.Condition, "System", participant.DisplayName,
                $"Cannot move to ({newGridX}, {newGridY}). Requires {movementCost} ft, has {moveSpeed} ft.");
            return combat;
        }

        await SetParticipantPositionAsync(combatId, participantId, newGridX, newGridY);

        return combat;
    }

    public async Task<GridPosition?> GetParticipantPositionAsync(Guid combatId, Guid participantId)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId);
        if (participant == null) return null;

        var notes = GetNotes(participant);
        if (notes == null) return null;

        return new GridPosition
        {
            ParticipantId = participantId,
            GridX = notes.ContainsKey("gridX") ? Convert.ToInt32(notes["gridX"]) : 0,
            GridY = notes.ContainsKey("gridY") ? Convert.ToInt32(notes["gridY"]) : 0,
            DisplayName = participant.DisplayName,
            MoveSpeed = notes.ContainsKey("moveSpeed") ? Convert.ToInt32(notes["moveSpeed"]) : 30
        };
    }

    public async Task<List<GridPosition>> GetAdjacentPositionsAsync(Guid combatId, int gridX, int gridY, int range = 1)
    {
        var positions = new List<GridPosition>();

        for (int dx = -range; dx <= range; dx++)
        {
            for (int dy = -range; dy <= range; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                if (Math.Abs(dx) + Math.Abs(dy) > range) continue;

                positions.Add(new GridPosition
                {
                    GridX = gridX + dx,
                    GridY = gridY + dy
                });
            }
        }

        return positions;
    }

}
