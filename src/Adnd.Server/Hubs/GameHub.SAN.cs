using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Adnd.Server.Events;
using System.Text.Json;

namespace Adnd.Server.Hubs;

public partial class GameHub
{
    // ==================== SAN (CoC) ====================

    public async Task<CombatLogResponse> CombatApplySANLoss(Guid combatId, Guid participantId, int sanLoss, string reason)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var result = await _combatService.ApplySANLossAsync(combatId, participantId, sanLoss, reason);
        await Clients.Group(combat.GameId.ToString()).SendAsync("CombatSANLoss", new
        {
            participantId,
            sanLoss,
            reason
        });
        return BuildCombatLog(result);
    }

    public async Task<CombatLogResponse> CombatApplySANRecovery(Guid combatId, Guid participantId, int sanRecovery)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var result = await _combatService.ApplySANRecoveryAsync(combatId, participantId, sanRecovery);
        await Clients.Group(combat.GameId.ToString()).SendAsync("CombatSANRecovery", new
        {
            participantId,
            sanRecovery
        });
        return BuildCombatLog(result);
    }

    public async Task<CombatSANCheckResponse> CombatMakeSANCheck(Guid combatId, Guid participantId, int dc)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var result = await _combatService.MakeSANCheckAsync(combatId, participantId, dc);

        await Clients.Group(combat.GameId.ToString()).SendAsync("CombatSANCheck", new
        {
            result.Participant,
            result.CurrentSAN,
            result.Roll,
            result.DC,
            result.Success,
            result.IsCritical,
            result.SANLoss,
            result.Effect
        });

        return new CombatSANCheckResponse
        {
            Participant = result.Participant,
            CurrentSAN = result.CurrentSAN,
            Roll = result.Roll,
            DC = result.DC,
            Success = result.Success,
            IsCritical = result.IsCritical,
            SANLoss = result.SANLoss,
            Effect = result.Effect
        };
    }

}
