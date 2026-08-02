using Adnd.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Hubs;

public partial class GameHub
{
    public async Task<List<MessageDto>> GetWhisperHistory(Guid gameId)
    {
        var player = await RequireMemberAsync(gameId);
        var session = await ResolveGameSessionAsync(gameId);
        var whispers = await db.Messages
            .Where(m => m.SessionId == session.Id && m.Type == "Whisper" &&
                        (m.WhisperFromId == player.Id || m.WhisperToId == player.Id))
            .OrderByDescending(m => m.CreatedAt)
            .Take(50)
            .ToListAsync();
        return whispers.Select(m => new MessageDto(m.Id, m.SessionId, m.PlayerId, m.Content, m.Type, false, m.CreatedAt, null)).ToList();
    }

    public async Task SendGMWhisper(Guid gameId, Guid targetPlayerId, string content)
    {
        // Speaking as the GM is creator-only — this had no check at all, so any
        // authenticated user could impersonate the Game Master in any game.
        var gm = await RequireCreatorAsync(gameId);

        var target = await db.Players
            .FirstOrDefaultAsync(p => p.Id == targetPlayerId && p.GameId == gameId)
            ?? throw new HubForbiddenException("Target player is not in this game.");

        var session = await ResolveGameSessionAsync(gameId);
        var msg = new Message
        {
            SessionId = session.Id,
            Content = content,
            Type = "Whisper",
            WhisperFromId = gm.Id,
            WhisperToId = targetPlayerId,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Messages.Add(msg);
        await db.SaveChangesAsync();

        var dto = new MessageDto(msg.Id, session.Id, gm.Id, content, "Whisper", false, msg.CreatedAt, null);
        await Clients.User(target.UserId.ToString()).SendCoreAsync("NewMessage", [dto]);
        await Clients.Caller.SendCoreAsync("NewMessage", [dto]);
    }
}
