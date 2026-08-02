using Adnd.Server.Data;
using Adnd.Server.Hubs;
using Adnd.Server.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Services;

public interface IRollPromptService
{
    /// <summary>
    /// Posts a question addressed to one player — "roll this", "spend an ability?" — as a real
    /// chat message rather than a banner floating above the log. Returns its id so the answer
    /// can replace it in place.
    /// </summary>
    Task<Guid> AskAsync(Guid gameId, Guid sessionId, Guid targetPlayerId, string type, string content, object metadata, CancellationToken ct = default);

    /// <summary>
    /// Turns the prompt into its outcome, keeping the same row so it stays where the player is
    /// already looking. <paramref name="makePublic"/> drops the private routing, which is how a
    /// question only one player could see becomes a result the whole table can.
    /// </summary>
    Task ResolveAsync(Guid gameId, Guid messageId, string type, string content, object? metadata, bool makePublic, CancellationToken ct = default);

    /// <summary>Withdraws a prompt that resolved into nothing worth showing.</summary>
    Task WithdrawAsync(Guid gameId, Guid messageId, CancellationToken ct = default);
}

/// <summary>
/// Roll requests and reroll offers live in the chat log, not in a banner. A banner is
/// modeless and undated: it says "the GM wants a roll" with no record of when that was asked
/// or what came of it, and it disappears the moment it is answered. As messages they sit in
/// the transcript in order, and the answer replaces the question in place.
///
/// The prompt carries whisper routing so it reaches only the player it is addressed to —
/// which also keeps it out of the narrator's context, since <c>MessageVisibility</c> excludes
/// whispers. The GM asked the question; it does not need to be told it asked.
/// </summary>
public class RollPromptService(AppDbContext db, IHubContext<GameHub> hub) : IRollPromptService
{
    public async Task<Guid> AskAsync(
        Guid gameId, Guid sessionId, Guid targetPlayerId, string type, string content, object metadata, CancellationToken ct = default)
    {
        var msg = new Message
        {
            SessionId = sessionId,
            Content = content,
            Type = type,
            Metadata = System.Text.Json.JsonSerializer.SerializeToElement(metadata),
            WhisperToId = targetPlayerId,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Messages.Add(msg);
        await db.SaveChangesAsync(ct);

        var userId = await db.Players
            .Where(p => p.Id == targetPlayerId)
            .Select(p => (Guid?)p.UserId)
            .FirstOrDefaultAsync(ct);

        if (userId is { } uid)
        {
            var dto = new MessageDto(msg.Id, sessionId, null, content, type, false, msg.CreatedAt, metadata);
            await hub.Clients.User(uid.ToString()).SendAsync("NewMessage", dto, ct);
        }

        return msg.Id;
    }

    public async Task ResolveAsync(
        Guid gameId, Guid messageId, string type, string content, object? metadata, bool makePublic, CancellationToken ct = default)
    {
        var msg = await db.Messages.FirstOrDefaultAsync(m => m.Id == messageId, ct);
        if (msg is null) return;

        var previousTarget = msg.WhisperToId;

        msg.Content = content;
        msg.Type = type;
        msg.Metadata = metadata is null
            ? default
            : System.Text.Json.JsonSerializer.SerializeToElement(metadata);

        if (makePublic)
        {
            msg.WhisperToId = null;
            msg.WhisperFromId = null;
        }

        await db.SaveChangesAsync(ct);

        var dto = new MessageDto(msg.Id, msg.SessionId, msg.PlayerId, content, type, false, msg.CreatedAt, metadata);

        if (makePublic)
        {
            await hub.Clients.Group(gameId.ToString()).SendAsync("MessageUpdated", dto, ct);
            return;
        }

        // Still private: only the player who was asked ever had this row, so only they are
        // told it changed. Broadcasting would hand the answer to everyone.
        if (previousTarget is { } targetPlayerId)
        {
            var userId = await db.Players
                .Where(p => p.Id == targetPlayerId)
                .Select(p => (Guid?)p.UserId)
                .FirstOrDefaultAsync(ct);

            if (userId is { } uid)
                await hub.Clients.User(uid.ToString()).SendAsync("MessageUpdated", dto, ct);
        }
    }

    public async Task WithdrawAsync(Guid gameId, Guid messageId, CancellationToken ct = default)
    {
        var msg = await db.Messages.FirstOrDefaultAsync(m => m.Id == messageId, ct);
        if (msg is null) return;

        var target = msg.WhisperToId;
        msg.IsDeleted = true;
        msg.DeletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        if (target is { } targetPlayerId)
        {
            var userId = await db.Players
                .Where(p => p.Id == targetPlayerId)
                .Select(p => (Guid?)p.UserId)
                .FirstOrDefaultAsync(ct);

            if (userId is { } uid)
                await hub.Clients.User(uid.ToString()).SendAsync("MessageRemoved", new { messageId }, ct);
        }
        else
        {
            await hub.Clients.Group(gameId.ToString()).SendAsync("MessageRemoved", new { messageId }, ct);
        }
    }
}
