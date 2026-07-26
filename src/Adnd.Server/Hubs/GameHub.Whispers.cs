using Adnd.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Hubs;

public partial class GameHub
{
    public async Task<List<MessageDto>> GetWhisperHistory(Guid gameId)
    {
        var userId = CurrentUserId;
        var player = await db.Players.FirstOrDefaultAsync(p => p.GameId == gameId && p.UserId == userId);
        if (player == null) return [];
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
        var session = await ResolveGameSessionAsync(gameId);
        var msg = new Message
        {
            SessionId = session.Id,
            Content = content,
            Type = "Whisper",
            WhisperToId = targetPlayerId,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Messages.Add(msg);
        await db.SaveChangesAsync();
        var dto = new MessageDto(msg.Id, session.Id, null, content, "Whisper", false, msg.CreatedAt, null);
        var target = await db.Players.Include(p => p.User).FirstOrDefaultAsync(p => p.Id == targetPlayerId);
        if (target != null)
        {
            var conns = _playerConnections.Where(kvp => kvp.Value == target.UserId.ToString()).Select(kvp => kvp.Key).ToList();
            if (conns.Count > 0) await Clients.Clients(conns).SendCoreAsync("NewMessage", [dto]);
        }
        await Clients.Caller.SendCoreAsync("NewMessage", [dto]);
    }
}
