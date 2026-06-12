using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Adnd.Server.Events;
using System.Text.Json;

namespace Adnd.Server.Hubs;

public partial class GameHub
{
    // ==================== AI Combat ====================

    public async Task<CombatAISuggestionsResponse> CombatGetAISuggestions(Guid combatId)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var suggestions = await _combatService.GetAITacticalSuggestionsAsync(combatId);

        await Clients.Group(combat.GameId.ToString()).SendAsync("CombatAISuggestions", new
        {
            combatId,
            suggestions.ThreatLevel,
            suggestions.RecommendedStrategy,
            suggestions.Suggestions,
            suggestions.NPCActions,
            suggestions.Warnings
        });

        return new CombatAISuggestionsResponse
        {
            CombatId = combatId,
            ThreatLevel = suggestions.ThreatLevel,
            RecommendedStrategy = suggestions.RecommendedStrategy,
            Suggestions = suggestions.Suggestions,
            NPCActions = suggestions.NPCActions,
            Warnings = suggestions.Warnings
        };
    }

    public async Task<CombatAISuggestionsResponse> CombatGetAINPCBehavior(Guid combatId, Guid npcId)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var suggestions = await _combatService.GetAINPCBehaviorAsync(combatId, npcId);

        await Clients.Group(combat.GameId.ToString()).SendAsync("CombatAINPCBehavior", new
        {
            combatId,
            npcId,
            suggestions.NPCActions
        });

        return new CombatAISuggestionsResponse
        {
            CombatId = combatId,
            NPCActions = suggestions.NPCActions
        };
    }

    public async Task<CombatLogResponse> CombatAutoResolve(Guid combatId, string resolutionMode = "quick")
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var result = await _combatService.AutoResolveCombatAsync(combatId, resolutionMode);
        await Clients.Group(combat.GameId.ToString()).SendAsync("CombatAutoResolved", new
        {
            combatId,
            resolutionMode
        });
        return BuildCombatLog(result);
    }

}
