using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Adnd.Server.Events;
using System.Text.Json;

namespace Adnd.Server.Hubs;

public partial class GameHub
{
    // ==================== Flavor Text Helpers ====================
    // Short, evocative text for immediate broadcast alongside mechanical results.
    // The GM agent adds deeper narrative separately via AgentCalls.

    private string GenerateFlavorText(string eventType, params object?[] args)
    {
        return eventType switch
        {
            "dice" => GenerateDiceFlavor((string?)args[0], (int?)args[1]),
            "skillcheck" => GenerateSkillCheckFlavor(
                (string?)args[1], (int?)args[2], (int?)args[3], (bool?)args[4]),
            "attack" => GenerateAttackFlavor(
                (string?)args[1], (string?)args[2], (bool?)args[3], (int?)args[4]),
            _ => ""
        };
    }

    private string GenerateDiceFlavor(string? formula, int? total)
    {
        if (string.IsNullOrEmpty(formula) || total == null) return "";
        var rolls = formula.Split('+').Select(f => f.Trim()).ToArray();
        var lastRoll = rolls.LastOrDefault();
        return lastRoll?.Contains("d") == true
            ? $"Rolling {lastRoll}... total: {total}"
            : $"Roll: {total}";
    }

    private string GenerateSkillCheckFlavor(string? skill, int? total, int? dc, bool? success)
    {
        if (string.IsNullOrEmpty(skill) || total == null) return "";
        var result = success == true ? "succeeds" : "fails";
        var dcText = dc != null ? $" (DC {dc})" : "";
        return $"{skill} check: {total}{dcText} — {result}";
    }

    private string GenerateAttackFlavor(string? weapon, string? target, bool? hit, int? damage)
    {
        if (string.IsNullOrEmpty(weapon)) return "";
        var targetText = !string.IsNullOrEmpty(target) ? $" vs {target}" : "";
        return hit == true
            ? $"{weapon}{targetText} — hit! ({damage} damage)"
            : $"{weapon}{targetText} — miss!";
    }

    private string GenerateCombatAttackFlavor(CombatAttackResponse result)
    {
        if (string.IsNullOrEmpty(result.Weapon)) return "";
        var targetText = !string.IsNullOrEmpty(result.Target) ? $" on {result.Target}" : "";
        if (result.IsCritical == true)
            return $"{result.Attacker} {result.Weapon}{targetText} — CRITICAL! ({result.DamageTotal} damage)";
        if (result.IsFumble == true)
            return $"{result.Attacker} {result.Weapon}{targetText} — fumble!";
        if (result.Hit == true)
            return $"{result.Attacker} {result.Weapon}{targetText} — hit! ({result.DamageTotal} damage)";
        return $"{result.Attacker} {result.Weapon}{targetText} — miss!";
    }

    private string GenerateSaveThrowFlavor(string participant, string saveType, bool success, int dc)
    {
        var result = success ? "succeeds" : "fails";
        return $"{participant} {saveType} save (DC {dc}): {result}";
    }
}
