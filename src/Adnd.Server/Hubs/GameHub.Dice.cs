using Adnd.Server.Events;
using Adnd.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Hubs;

public partial class GameHub
{
    public async Task RollDice(Guid gameId, string formula, bool isSecret = false)
    {
        var player = await RequireMemberAsync(gameId);
        var session = await ResolveGameSessionAsync(gameId);

        var result = diceEngine.Roll(formula);
        var content = $"Rolled {formula}: {result.Total} ({result.Breakdown})";
        var metadata = new { formula = result.Formula, total = result.Total, breakdown = result.Breakdown };

        var msg = new Message
        {
            SessionId = session.Id,
            PlayerId = player.Id,
            Content = content,
            Type = "DiceRoll",
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Messages.Add(msg);
        await db.SaveChangesAsync();

        var dto = new MessageDto(msg.Id, session.Id, player.Id, content, "DiceRoll", false, msg.CreatedAt, metadata);
        if (isSecret)
            await Clients.Caller.SendCoreAsync("NewMessage", [dto]);
        else
            await BroadcastToGameAsync(gameId, "NewMessage", dto);

        await PublishAsync(new DiceRolled(gameId, player.Id, formula, result.Total, result.Breakdown));
    }

    public async Task RollSkillCheck(Guid gameId, string skillId, int dc, bool isSecret = false)
    {
        await RollDice(gameId, "1d20", isSecret);
        await PublishAsync(new SkillCheckRequested(gameId, CurrentUserId, skillId, dc));
    }

    public async Task RollAttack(Guid gameId, int attackBonus, Guid? targetId = null)
    {
        await RollDice(gameId, $"1d20+{attackBonus}");
        await PublishAsync(new AttackRequested(gameId, CurrentUserId, targetId, attackBonus));
    }
}
