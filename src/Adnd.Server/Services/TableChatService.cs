using Adnd.Server.Data;
using Adnd.Server.Dtos;
using Adnd.Server.Events;
using Adnd.Server.Hubs;
using Adnd.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Services;

/// <summary>Refused: the caller is not in the game, has no living character to speak as, and so on. The message is for the user.</summary>
public sealed class TableChatException(string message) : Exception(message);

/// <summary>
/// What a player says at the table, from whichever client: the web chat (<c>GameHub</c>) and the Telegram bot both
/// come through here, so the rules — membership, a living character to speak in character, whisper routing — are
/// one set.
/// </summary>
public interface ITableChatService
{
    /// <summary>The caller's seat in the game, or a <see cref="TableChatException"/>.</summary>
    Task<Player> RequireMemberAsync(Guid gameId, Guid userId, CancellationToken ct = default);

    /// <summary>A line to the whole table, in character or OOC.</summary>
    Task<Message> SayAsync(Guid gameId, Guid userId, string content, bool isOOC, CancellationToken ct = default);

    /// <summary>A private line to one other player.</summary>
    Task<Message> WhisperAsync(Guid gameId, Guid userId, Guid targetPlayerId, string content, bool isOOC, CancellationToken ct = default);

    /// <summary>Asks the GM to answer an in-character line: queues a GM turn (it waits while another is running).</summary>
    Task NarrateAsync(Guid gameId, Guid userId, string prompt, CancellationToken ct = default);

    /// <summary>A private out-of-character question to the GM assistant; the answer is whispered back.</summary>
    Task AskGmAsync(Guid gameId, Guid userId, string prompt, CancellationToken ct = default);
}

public class TableChatService(
    AppDbContext db,
    ISessionManagementService sessions,
    IEventBus eventBus,
    IAgentBus agentBus,
    IRAGService rag,
    IGameNotifier notifier) : ITableChatService
{
    /// <summary>
    /// Every entry point must call this before touching a game. Only JoinGameGroup used to verify membership, and
    /// Clients.Group() does not require the caller to be in the group — so any authenticated user could post,
    /// whisper as the GM, drive combat, or spend another user's LLM budget in a game they had nothing to do with.
    /// </summary>
    public async Task<Player> RequireMemberAsync(Guid gameId, Guid userId, CancellationToken ct = default)
        => await db.Players.FirstOrDefaultAsync(p => p.GameId == gameId && p.UserId == userId, ct)
           ?? throw new TableChatException("You are not a member of this game.");

    public async Task<Message> SayAsync(Guid gameId, Guid userId, string content, bool isOOC, CancellationToken ct = default)
    {
        var player = await RequireMemberAsync(gameId, userId, ct);

        // In-character speech implies a character to speak as; OOC table talk does not.
        // Client-side already warns via a banner, but that's UI-only — enforce it here too.
        if (!isOOC)
        {
            var speaker = await db.Characters.FirstOrDefaultAsync(c => c.PlayerId == player.Id, ct)
                ?? throw new TableChatException("Create a character before speaking in character.");
            // The dead do not act, and an in-character line starts a GM turn.
            if (CharacterStateService.IsDead(speaker))
                throw new TableChatException($"{speaker.Name} is dead. Create a new character to rejoin the story — you can still talk out of character.");
        }

        var session = await sessions.GetOrCreateCurrentSessionAsync(gameId);

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
        await db.SaveChangesAsync(ct);

        await notifier.ToGameAsync(gameId, "NewMessage", Dto(msg), ct);
        await eventBus.PublishAsync(new MessageSent(gameId, session.Id, player.Id, content, msg.Type), ct);
        return msg;
    }

    public async Task<Message> WhisperAsync(Guid gameId, Guid userId, Guid targetPlayerId, string content, bool isOOC, CancellationToken ct = default)
    {
        var player = await RequireMemberAsync(gameId, userId, ct);

        // The target must be in the same game, or this becomes a cross-game message channel.
        var targetPlayer = await db.Players
            .FirstOrDefaultAsync(p => p.Id == targetPlayerId && p.GameId == gameId, ct)
            ?? throw new TableChatException("Target player is not in this game.");

        var session = await sessions.GetOrCreateCurrentSessionAsync(gameId);

        var msg = new Message
        {
            SessionId = session.Id,
            PlayerId = player.Id,
            Content = content,
            // OOC is a modifier on a whisper as much as on a public line — the client already
            // renders OOCWhisper, but this used to store every whisper as plain "Whisper".
            Type = isOOC ? "OOCWhisper" : "Whisper",
            IsOOC = isOOC,
            WhisperFromId = player.Id,
            WhisperToId = targetPlayerId,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Messages.Add(msg);
        await db.SaveChangesAsync(ct);

        var dto = Dto(msg);
        await notifier.ToUserAsync(gameId, userId, "NewMessage", dto, ct);
        if (targetPlayer.UserId != userId)
            await notifier.ToUserAsync(gameId, targetPlayer.UserId, "NewMessage", dto, ct);
        return msg;
    }

    public async Task NarrateAsync(Guid gameId, Guid userId, string prompt, CancellationToken ct = default)
    {
        // Queues a billable LLM call against the game owner's API key.
        var speaker = await RequireMemberAsync(gameId, userId, ct);
        var session = await sessions.GetOrCreateCurrentSessionAsync(gameId);

        // The prompt is built when the turn starts, not here: a line sent while the GM is
        // still resolving the previous one waits its turn, and must then be answered with the
        // world as that turn left it.
        var call = new AgentCall
        {
            GameId = gameId,
            SessionId = session.Id,
            FromAgent = AgentType.Player,
            ToAgent = AgentType.GM,
            Action = AgentAction.Narrate,
            Input = System.Text.Json.JsonSerializer.Serialize(
                new GMDispatchOptions("", prompt, Pending: GMDispatchOptions.PendingTurn, SpeakerPlayerId: speaker.Id))
        };
        await agentBus.SendCallAsync(call);
    }

    public async Task AskGmAsync(Guid gameId, Guid userId, string prompt, CancellationToken ct = default)
    {
        var player = await RequireMemberAsync(gameId, userId, ct);
        var session = await sessions.GetOrCreateCurrentSessionAsync(gameId);

        // TriggerSuggest never saved the player's own question, so it just vanished from
        // their chat log the moment they sent it. WhisperFromId marks it private to them —
        // no one else in the game should see a player quietly asking the GM assistant OOC.
        var questionMsg = new Message
        {
            SessionId = session.Id,
            PlayerId = player.Id,
            Content = prompt,
            Type = "OOC",
            IsOOC = true,
            WhisperFromId = player.Id,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Messages.Add(questionMsg);
        await db.SaveChangesAsync(ct);
        await notifier.ToUserAsync(gameId, userId, "NewMessage", Dto(questionMsg), ct);

        var game = await db.Games.FirstOrDefaultAsync(g => g.Id == gameId, ct);
        // This bare a one-liner gave the model no directive to actually answer — observed
        // live: it reasoned at length about who an NPC was and then emitted zero response
        // text, producing a blank chat bubble. Being explicit about answering out-of-character,
        // concisely, and without invoking tools closes off the modes that led there.
        var systemPrompt = "You are an AI GM assistant answering an out-of-character question from a player. " +
            "Respond directly and concisely in plain prose — do not narrate in character and do not call any tools. " +
            "Always give a real answer; if you're unsure, say so plainly rather than leaving the response empty.";
        if (game?.LanguageDirective is { } languageDirective)
            systemPrompt = $"{systemPrompt} {languageDirective}";

        var context = await rag.GeneratePlotContextAsync(gameId);
        var userPrompt = string.IsNullOrEmpty(context)
            ? prompt
            : $"{context}\n\n=== CURRENT REQUEST ===\n{prompt}";

        var options = new GMDispatchOptions(systemPrompt, userPrompt);
        var call = new AgentCall
        {
            GameId = gameId,
            SessionId = session.Id,
            FromAgent = AgentType.Player,
            ToAgent = AgentType.GM,
            Action = AgentAction.Suggest,
            RequestedByPlayerId = player.Id,
            Input = System.Text.Json.JsonSerializer.Serialize(options)
        };
        await agentBus.SendCallAsync(call);
    }

    private static MessageDto Dto(Message m) =>
        new(m.Id, m.SessionId, m.PlayerId, m.Content, m.Type, m.IsOOC, m.CreatedAt, null);
}
