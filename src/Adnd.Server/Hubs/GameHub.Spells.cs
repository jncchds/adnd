using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Adnd.Server.Events;
using System.Text.Json;

namespace Adnd.Server.Hubs;

public partial class GameHub
{
    // ==================== Spell Combat ====================

    public async Task<CombatSpellCastResponse> CombatCastSpell(Guid combatId, string casterName, string spellName,
        Guid targetId, string saveFormula, int saveDC, string? damageFormula = null,
        int? damageBonus = null, string? description = null)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var result = await _combatService.CastSpellAsync(
            combatId, casterName, spellName, targetId, saveFormula, saveDC,
            damageFormula, damageBonus, description);

        await Clients.Group(combat.GameId.ToString()).SendAsync("CombatSpellCast", new
        {
            result.Caster,
            result.SpellName,
            result.SpellLevel,
            result.Target,
            result.SaveType,
            result.SaveDC,
            result.SaveSuccess,
            result.IsCritical,
            result.DamageType,
            result.DamageTotal,
            result.DamageInfo,
            result.Effect,
            result.TargetHP,
            result.TargetMaxHP
        });

        return new CombatSpellCastResponse
        {
            Caster = result.Caster,
            SpellName = result.SpellName,
            SpellLevel = result.SpellLevel,
            Target = result.Target,
            SaveType = result.SaveType,
            SaveDC = result.SaveDC,
            SaveSuccess = result.SaveSuccess,
            IsCritical = result.IsCritical,
            DamageType = result.DamageType,
            DamageTotal = result.DamageTotal,
            DamageInfo = result.DamageInfo,
            Effect = result.Effect,
            TargetHP = result.TargetHP,
            TargetMaxHP = result.TargetMaxHP
        };
    }

    public async Task<CombatSpellCastResponse> CombatCastAreaSpell(Guid combatId, string casterName, string spellName,
        string saveFormula, int saveDC, string? damageFormula = null,
        int? damageBonus = null, string? description = null, string[]? targetIds = null)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var targetGuids = targetIds?.Select(Guid.Parse).ToArray();
        var result = await _combatService.CastAreaSpellAsync(
            combatId, casterName, spellName, saveFormula, saveDC,
            damageFormula, damageBonus, description, targetGuids);

        await Clients.Group(combat.GameId.ToString()).SendAsync("CombatSpellCast", new
        {
            result.Caster,
            result.SpellName,
            result.Target,
            result.SaveType,
            result.SaveDC,
            result.SaveSuccess,
            result.DamageType,
            result.DamageTotal,
            result.DamageInfo,
            result.Effect
        });

        return new CombatSpellCastResponse
        {
            Caster = result.Caster,
            SpellName = result.SpellName,
            Target = result.Target,
            SaveType = result.SaveType,
            SaveDC = result.SaveDC,
            SaveSuccess = result.SaveSuccess,
            DamageType = result.DamageType,
            DamageTotal = result.DamageTotal,
            DamageInfo = result.DamageInfo,
            Effect = result.Effect
        };
    }

}
