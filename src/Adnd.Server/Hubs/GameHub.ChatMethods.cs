using Adnd.Server.Events;
using Adnd.Server.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Hubs;

public partial class GameHub
{
    public async Task SendMessage(Guid gameId, string content, bool isOOC = false)
    {
        var userId = CurrentUserId;
        var player = await db.Players.FirstOrDefaultAsync(p => p.GameId == gameId && p.UserId == userId);
        var session = await ResolveGameSessionAsync(gameId);

        await PersistGameEventAsync(session.Id, content, isOOC ? "OOC" : "Chat", player?.Id, isOOC);

        var msg = await db.Messages.OrderByDescending(m => m.CreatedAt)
            .FirstOrDefaultAsync(m => m.SessionId == session.Id);
        if (msg != null)
        {
            var dto = new MessageDto(msg.Id, msg.SessionId, msg.PlayerId, msg.Content, msg.Type, msg.IsOOC, msg.CreatedAt, null);
            await BroadcastToGameAsync(gameId, "NewMessage", dto);
        }

        await PublishAsync(new MessageSent(gameId, session.Id, player?.Id, content, isOOC ? "OOC" : "Chat"));
    }

    public async Task SendWhisper(Guid gameId, Guid targetPlayerId, string content)
    {
        var userId = CurrentUserId;
        var player = await db.Players.FirstOrDefaultAsync(p => p.GameId == gameId && p.UserId == userId);
        var session = await ResolveGameSessionAsync(gameId);

        var msg = new Message
        {
            SessionId = session.Id,
            PlayerId = player?.Id,
            Content = content,
            Type = "Whisper",
            WhisperFromId = player?.Id,
            WhisperToId = targetPlayerId,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Messages.Add(msg);
        await db.SaveChangesAsync();

        var dto = new MessageDto(msg.Id, msg.SessionId, msg.PlayerId, msg.Content, msg.Type, false, msg.CreatedAt, null);
        await Clients.Caller.SendCoreAsync("NewMessage", [dto]);

        var targetPlayer = await db.Players.Include(p => p.User).FirstOrDefaultAsync(p => p.Id == targetPlayerId);
        if (targetPlayer != null)
        {
            var targetConns = _playerConnections
                .Where(kvp => kvp.Value == targetPlayer.UserId.ToString())
                .Select(kvp => kvp.Key).ToList();
            if (targetConns.Count > 0)
                await Clients.Clients(targetConns).SendCoreAsync("NewMessage", [dto]);
        }
    }

    public async Task SendOOCMessage(Guid gameId, string content)
        => await SendMessage(gameId, content, isOOC: true);

    public async Task SendOOCWhisper(Guid gameId, Guid targetPlayerId, string content)
        => await SendWhisper(gameId, targetPlayerId, content);
}
