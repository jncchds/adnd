using Adnd.Server.Dtos;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Hubs;

public partial class GameHub
{
    public async Task TriggerNarrate(Guid gameId, string prompt)
    {
        // Queues a billable LLM call against the game owner's API key.
        var speaker = await RequireMemberAsync(gameId);
        var session = await ResolveGameSessionAsync(gameId);

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

    public async Task TriggerSuggest(Guid gameId, string prompt)
    {
        var player = await RequireMemberAsync(gameId);
        var session = await ResolveGameSessionAsync(gameId);

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
        await db.SaveChangesAsync();
        var questionDto = new MessageDto(questionMsg.Id, questionMsg.SessionId, questionMsg.PlayerId,
            questionMsg.Content, questionMsg.Type, questionMsg.IsOOC, questionMsg.CreatedAt, null);
        await Clients.Caller.SendCoreAsync("NewMessage", [questionDto]);

        var game = await db.Games.FirstOrDefaultAsync(g => g.Id == gameId);
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
}
