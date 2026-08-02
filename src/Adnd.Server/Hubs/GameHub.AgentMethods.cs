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
        await RequireMemberAsync(gameId);
        var session = await ResolveGameSessionAsync(gameId);
        var systemPrompt = await BuildNarrateSystemPromptAsync(gameId);

        // Without recent-message context each narrate call only ever saw the single line the
        // player just sent, with no memory of the last several exchanges — the GM re-invented
        // the scene from scratch on every turn instead of continuing it.
        var context = await rag.GeneratePlotContextAsync(gameId);
        var userPrompt = string.IsNullOrEmpty(context)
            ? prompt
            : $"{context}\n\n=== CURRENT ACTION ===\n{prompt}";

        var options = new GMDispatchOptions(
            SystemPrompt: systemPrompt,
            UserPrompt: userPrompt);
        var call = new AgentCall
        {
            GameId = gameId,
            SessionId = session.Id,
            FromAgent = AgentType.Player,
            ToAgent = AgentType.GM,
            Action = AgentAction.Narrate,
            Input = System.Text.Json.JsonSerializer.Serialize(options)
        };
        await agentBus.SendCallAsync(call);
    }

    private async Task<string> BuildNarrateSystemPromptAsync(Guid gameId)
    {
        var game = await db.Games.Include(g => g.Players).ThenInclude(p => p.User)
            .FirstOrDefaultAsync(g => g.Id == gameId);
        if (game == null) return "You are the AI Game Master for an ongoing TTRPG session.";

        var plots = await db.PlotThreads
            .Where(p => p.GameId == gameId && !p.IsDeleted && p.Status == PlotThreadStatus.Active)
            .OrderByDescending(p => p.Momentum)
            .Take(4)
            .ToListAsync();

        var characters = await db.Characters
            .Where(c => c.Player.GameId == gameId && !c.IsDeleted)
            .Include(c => c.Player)
            .ToListAsync();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("You are the AI Game Master for an ongoing TTRPG session. Respond in character at all times.");
        sb.AppendLine("CRITICAL: Never break the fourth wall. Never mention context retrieval, system limitations, or technical issues. If you lack information, improvise based on established fiction.");
        sb.AppendLine();
        sb.AppendLine($"Game: {game.Name} | System: {game.SystemId}");

        if (game.LanguageDirective is { } languageDirective)
            sb.AppendLine(languageDirective);

        if (!string.IsNullOrEmpty(game.PlotSeed))
        {
            sb.AppendLine($"Campaign seed: {game.PlotSeed}");
            sb.AppendLine();
        }

        if (plots.Count > 0)
        {
            sb.AppendLine("Active plot threads:");
            foreach (var p in plots)
                sb.AppendLine($"- [{p.Category}] {p.Title}: {p.Description}");
            sb.AppendLine();
        }

        if (characters.Count > 0)
        {
            sb.AppendLine("Player characters:");
            foreach (var c in characters)
            {
                // Ability modifiers ("STR +2, DEX +0, …") so the LLM can write a real number
                // into a dice formula itself instead of inventing a placeholder like
                // "1d20+{strength}", which DiceEngine can't resolve.
                var mods = AbilityScoreHelper.FormatModifiers(c.Attributes);
                var modsSuffix = string.IsNullOrEmpty(mods) ? "" : $" — {mods}";
                sb.AppendLine($"- {c.Name} (Level {c.Level} {c.Class}, HP {c.CurrentHP}/{c.MaxHP}){modsSuffix}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("Narrate the scene vividly. Use the available tools (rollDice, skillCheck, startCombat, etc.) when appropriate. Keep responses concise and end with an open question or clear call to action.");
        sb.AppendLine("When a roll is needed, use a fully-resolved dice formula such as \"1d20+3\" — add the relevant ability modifier listed above yourself. Never write a placeholder like \"1d20+{strength}\"; the dice engine cannot resolve it.");
        return sb.ToString();
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

    public async Task<string?> GetGMStatus(Guid gameId)
    {
        await RequireMemberAsync(gameId);
        return await agentBus.GetGMStatusAsync(gameId);
    }

    // Halting the GM is a denial of service against everyone else at the table — creator only.
    public async Task PauseGM(Guid gameId)
    {
        await RequireCreatorAsync(gameId);
        await agentBus.PauseGMAsync(gameId);
        await BroadcastToGameAsync(gameId, "GMStatusChanged", new GMStatusDto(gameId, "Paused", null));
    }

    public async Task ResumeGM(Guid gameId)
    {
        await RequireCreatorAsync(gameId);
        await agentBus.ResumeGMAsync(gameId);
        await BroadcastToGameAsync(gameId, "GMStatusChanged", new GMStatusDto(gameId, "Running", null));
    }
}
