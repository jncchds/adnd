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

        // Publish event for game agent processing
        await _mediator.Publish(new DiceRolled(session.GameId, sessionId, formula, playerId));

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

        // Publish event for game agent processing
        await _mediator.Publish(new SkillCheckRequested(session.GameId, sessionId, skill, playerId, dc));

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

        // Publish event for game agent processing
        await _mediator.Publish(new AttackRequested(session.GameId, sessionId, weapon, targetName, playerId));

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
