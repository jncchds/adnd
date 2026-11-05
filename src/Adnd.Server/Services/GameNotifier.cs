using Adnd.Server.Hubs;
using Adnd.Server.Telegram;
using Microsoft.AspNetCore.SignalR;

namespace Adnd.Server.Services;

/// <summary>Payload of <c>MessageRemoved</c> (<c>{ messageId }</c> on the wire).</summary>
public record MessageRemovedDto(Guid MessageId);

/// <summary>Payload of <c>GMError</c> (<c>{ message }</c> on the wire).</summary>
public record GMErrorDto(string Message);

/// <summary>
/// The one way game events leave the server: to the web clients over SignalR, and a copy to the Telegram bot.
/// Send through here rather than <c>IHubContext&lt;GameHub&gt;</c> — a push that skips it never reaches the chats
/// that follow the game in Telegram. The SignalR send goes first and stays the one that can fail the call; the
/// Telegram copy never blocks or throws.
/// </summary>
public interface IGameNotifier
{
    /// <summary>To everyone in the game's group.</summary>
    Task ToGameAsync(Guid gameId, string method, object payload, CancellationToken ct = default);

    /// <summary>To one user, about something in <paramref name="gameId"/>.</summary>
    Task ToUserAsync(Guid gameId, Guid userId, string method, object payload, CancellationToken ct = default);
}

public class GameNotifier(IHubContext<GameHub> hub, TelegramEventSink telegram) : IGameNotifier
{
    public async Task ToGameAsync(Guid gameId, string method, object payload, CancellationToken ct = default)
    {
        await hub.Clients.Group(gameId.ToString()).SendAsync(method, payload, ct);
        Forward(gameId, method, payload);
    }

    public async Task ToUserAsync(Guid gameId, Guid userId, string method, object payload, CancellationToken ct = default)
    {
        await hub.Clients.User(userId.ToString()).SendAsync(method, payload, ct);
        Forward(gameId, method, payload);
    }

    /// <summary>
    /// The bot reads every message back from the database and decides per chat who may see it, so a message pushed
    /// to one user and the same message pushed to the group are one event to it.
    /// </summary>
    private void Forward(Guid gameId, string method, object payload)
    {
        switch (method, payload)
        {
            case ("NewMessage" or "MessageUpdated", MessageDto m):
                telegram.Post(new TgMessageEvent(gameId, m.Id));
                break;
            case ("MessageRemoved", MessageRemovedDto r):
                telegram.Post(new TgMessageEvent(gameId, r.MessageId));
                break;
            case ("CombatUpdated" or "CombatEnded", _):
                telegram.Post(new TgCombatEvent(gameId));
                break;
            case ("GMActivity", GMActivityDto a):
                telegram.Post(new TgActivityEvent(gameId, a.Step));
                break;
            case ("GMError", GMErrorDto e):
                telegram.Post(new TgErrorEvent(gameId, e.Message));
                break;
        }
    }
}
