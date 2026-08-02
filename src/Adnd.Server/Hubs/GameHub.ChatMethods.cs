using Adnd.Server.Events;
using Adnd.Server.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Hubs;

public partial class GameHub
{
    public async Task SendMessage(Guid gameId, string content, bool isOOC = false)
    {
        var player = await RequireMemberAsync(gameId);

        // In-character speech implies a character to speak as; OOC table talk does not.
        // Client-side already warns via a banner, but that's UI-only — enforce it here too.
        if (!isOOC && !await db.Characters.AnyAsync(c => c.PlayerId == player.Id))
            throw new HubForbiddenException("Create a character before speaking in character.");

        var session = await ResolveGameSessionAsync(gameId);

        // Build the entity here rather than re-querying "newest message in the session"
        // afterwards, which could return a concurrently-inserted message from someone else.
        var msg = new Message
        {
            SessionId = session.Id,
            PlayerId = player.Id,
            Content = content,
            Type = isOOC ? "OOC" : "Chat",
            IsOOC = isOOC,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Messages.Add(msg);
        await db.SaveChangesAsync();

        var dto = new MessageDto(msg.Id, msg.SessionId, msg.PlayerId, msg.Content, msg.Type, msg.IsOOC, msg.CreatedAt, null);
        await BroadcastToGameAsync(gameId, "NewMessage", dto);

        await PublishAsync(new MessageSent(gameId, session.Id, player.Id, content, msg.Type));
    }

    public async Task SendWhisper(Guid gameId, Guid targetPlayerId, string content)
    {
        var player = await RequireMemberAsync(gameId);

        // The target must be in the same game, or this becomes a cross-game message channel.
        var targetPlayer = await db.Players
            .FirstOrDefaultAsync(p => p.Id == targetPlayerId && p.GameId == gameId)
            ?? throw new HubForbiddenException("Target player is not in this game.");

        var session = await ResolveGameSessionAsync(gameId);

        var msg = new Message
        {
            SessionId = session.Id,
            PlayerId = player.Id,
            Content = content,
            Type = "Whisper",
            WhisperFromId = player.Id,
            WhisperToId = targetPlayerId,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Messages.Add(msg);
        await db.SaveChangesAsync();

        var dto = new MessageDto(msg.Id, msg.SessionId, msg.PlayerId, msg.Content, msg.Type, false, msg.CreatedAt, null);
        await Clients.Caller.SendCoreAsync("NewMessage", [dto]);
        await Clients.User(targetPlayer.UserId.ToString()).SendCoreAsync("NewMessage", [dto]);
    }

    public async Task SendOOCMessage(Guid gameId, string content)
        => await SendMessage(gameId, content, isOOC: true);

    public async Task SendOOCWhisper(Guid gameId, Guid targetPlayerId, string content)
        => await SendWhisper(gameId, targetPlayerId, content);
}
