using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Adnd.Server.Events;
using System.Text.Json;

namespace Adnd.Server.Hubs;

public partial class GameHub
{
    // ==================== Dice ====================

    public async Task RollDice(Guid sessionId, string formula, Guid? playerId = null)
    {
        var session = await _context.GameSessions.FindAsync(sessionId);
        if (session == null)
        {
            await Clients.Caller.SendAsync("Error", new { message = "Session not found." });
            return;
        }

        var result = await _gameEngine.RollDiceAsync(sessionId, formula, playerId);

        // Persist as unified chat message
        var metadata = JsonDocument.Parse($"{{\"formula\":\"{result.Formula}\",\"total\":{result.Total},\"rolls\":[{string.Join(",", result.Rolls)}]}}").RootElement;
        await PersistGameEventAsync(
            session.GameId, sessionId, playerId,
            playerId.HasValue ? "Player" : "System",
            $"🎲 **{result.Formula}** → **{result.Total}**{(result.FinalRolls != null && result.FinalRolls.Any() ? $" (kept: [{string.Join(",", result.FinalRolls)})" : "")}{(result.Modifier != 0 ? $" (modifier: {result.Modifier:+#;-#;0})" : "")}",
            Adnd.Server.Models.MessageType.Dice, metadata);

        // Publish event for game agent processing
        await _eventBus.PublishAsync(new DiceRolled(session.GameId, sessionId, formula, playerId));

        await Clients.Group(session.GameId.ToString()).SendAsync("DiceRollResult", new
        {
            Formula = result.Formula,
            DiceCount = result.DiceCount,
            DiceType = result.DiceType,
            Modifier = result.Modifier,
            Rolls = result.Rolls,
            FinalRolls = result.FinalRolls,
            Subtotal = result.Subtotal,
            Total = result.Total,
            PlayerId = playerId,
            Timestamp = result.RolledAt,
            FlavorText = GenerateFlavorText("dice", result.Formula, result.Total)
        });
    }

    // ==================== Skill Checks ====================

    public async Task SkillCheck(Guid sessionId, string skill, Guid? playerId = null, int? dc = null)
    {
        var session = await _context.GameSessions.FindAsync(sessionId);
        if (session == null)
        {
            await Clients.Caller.SendAsync("Error", new { message = "Session not found." });
            return;
        }

        var result = await _gameEngine.SkillCheckAsync(sessionId, skill, playerId, dc);

        // Persist as unified chat message
        var metadata = JsonDocument.Parse($"{{\"skill\":\"{result.Skill}\",\"dc\":{result.DC},\"success\":{result.Success.ToString().ToLower()},\"diceRoll\":{result.DiceRoll},\"modifier\":{result.Modifier},\"total\":{result.Total}}}").RootElement;
        await PersistGameEventAsync(
            session.GameId, sessionId, playerId,
            "System",
            $"📋 **{result.Skill} Check** vs DC {result.DC}: d20({result.DiceRoll})+{result.Modifier:+#;-#;0} = **{result.Total}** → {(result.Success ? "✅ Success" : "❌ Failure")}",
            Adnd.Server.Models.MessageType.SkillCheck, metadata);

        // Publish event for game agent processing (async — queues GM narrative)
        PublishAsync(new SkillCheckRequested(session.GameId, sessionId, skill, playerId, dc));

        await Clients.Group(session.GameId.ToString()).SendAsync("SkillCheckResult", new
        {
            Skill = result.Skill,
            DiceRoll = result.DiceRoll,
            Modifier = result.Modifier,
            Total = result.Total,
            DC = result.DC,
            Success = result.Success,
            RolledAt = result.RolledAt,
            FlavorText = GenerateFlavorText("skillcheck", result.Skill, result.Total, result.DC, result.Success)
        });
    }

    // ==================== Attacks ====================

    public async Task Attack(Guid sessionId, string weapon, string targetName, Guid? playerId = null)
    {
        var session = await _context.GameSessions.FindAsync(sessionId);
        if (session == null)
        {
            await Clients.Caller.SendAsync("Error", new { message = "Session not found." });
            return;
        }

        var result = await _gameEngine.AttackAsync(sessionId, weapon, targetName, playerId);

        // Persist as unified chat message
        var metadata = JsonDocument.Parse($"{{\"weapon\":\"{result.Weapon}\",\"target\":\"{result.Target}\",\"hit\":{result.Hit.ToString().ToLower()},\"attackRoll\":{result.AttackRoll},\"ac\":{result.AC},\"damageTotal\":{result.DamageTotal}}}").RootElement;
        var attackResult = result.Hit ? $"✅ **HIT!** {result.DamageTotal} damage" : "❌ **MISS**";
        await PersistGameEventAsync(
            session.GameId, sessionId, playerId,
            "System",
            $"⚔️ **{result.Weapon}** vs **{result.Target}**: d20({result.AttackRoll}) vs AC {result.AC} → {attackResult}",
            Adnd.Server.Models.MessageType.Attack, metadata);

        // Publish event for game agent processing (async — queues GM narrative)
        PublishAsync(new AttackRequested(session.GameId, sessionId, weapon, targetName, playerId));

        await Clients.Group(session.GameId.ToString()).SendAsync("AttackResult", new
        {
            Weapon = result.Weapon,
            Target = result.Target,
            Hit = result.Hit,
            AttackRoll = result.AttackRoll,
            AC = result.AC,
            DamageDice = result.DamageDice,
            DamageTotal = result.DamageTotal,
            RolledAt = result.RolledAt,
            FlavorText = GenerateFlavorText("attack", result.Weapon, result.Target, result.Hit, result.DamageTotal)
        });
    }

}
