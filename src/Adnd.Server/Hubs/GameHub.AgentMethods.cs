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
        var systemPrompt = await BuildNarrateSystemPromptAsync(gameId, session.Id);

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

    private async Task<string> BuildNarrateSystemPromptAsync(Guid gameId, Guid sessionId)
    {
        var game = await db.Games.Include(g => g.Players).ThenInclude(p => p.User)
            .FirstOrDefaultAsync(g => g.Id == gameId);
        if (game == null) return "You are the AI Game Master for an ongoing TTRPG session.";

        var plots = await db.PlotThreads
            .Where(p => p.GameId == gameId && !p.IsDeleted && p.Status == PlotThreadStatus.Active)
            .OrderByDescending(p => p.Momentum)
            .Take(4)
            .ToListAsync();

        // Not every NPC in the campaign — only the ones the situation is about. See
        // NPCRelevanceService for what "relevant" means here.
        var npcs = await npcRelevance.GetRelevantAsync(gameId, sessionId);

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

        // The roster is what makes "register anyone not on this list" a usable instruction:
        // without it the model has no way to tell a new NPC from one it named three turns ago,
        // and either re-registers everybody or registers nobody.
        if (npcs.Count > 0)
        {
            sb.AppendLine("NPCs currently in play (already registered — do not re-register unless something about them changed). This is the cast relevant to the situation, not every NPC in the campaign; use queryNPCs if you need someone else:");
            foreach (var n in npcs)
            {
                var faction = string.IsNullOrEmpty(n.Faction) ? "" : $", {n.Faction}";
                var status = n.Status == NPCStatus.Active ? "" : $", {n.Status.ToString().ToUpperInvariant()}";
                sb.AppendLine($"- {n.Name} ({n.Attitude}{faction}{status})");
            }
            sb.AppendLine();
        }

        sb.AppendLine("Narrate the scene vividly when the scene calls for it. Use the available tools (rollDice, skillCheck, startCombat, etc.) when appropriate.");
        sb.AppendLine("When a roll is needed, use a fully-resolved dice formula such as \"1d20+3\" — add the relevant ability modifier listed above yourself. Never write a placeholder like \"1d20+{strength}\"; the dice engine cannot resolve it.");

        // NPCs used to exist only if the human GM typed them into the admin UI, so anyone the
        // narration introduced was forgotten the moment they scrolled out of the RAG window,
        // and came back next scene as a different person with a different attitude.
        sb.AppendLine("Whenever your narration names or introduces an NPC who is not already registered, call the \"registerNPC\" tool in the SAME response as your narration — one call per NPC, with a one-or-two-sentence description, their attitude toward the party, and their faction if they have one. Do the same when an established NPC's attitude, faction or situation changes. Registering a name that already exists updates that NPC rather than creating a duplicate, so it is always safe to call. Do not register unnamed background extras (a crowd, a guard the party walks past) — only NPCs the story is likely to return to.");

        // Without this the roster only ever grows, and the narrator keeps being handed people
        // the party killed two sessions ago as though they were still standing there.
        sb.AppendLine("When a registered NPC dies, or leaves the story for good — they move away, are written out, or the plot has passed them by — call \"updateNPCStatus\" in the same response with status Dead or Departed. They stay on record and remain available through queryNPCs; they just stop being listed as part of the current cast.");

        // "Keep responses concise and end with an open question or clear call to action" used to
        // apply to every single narrate call, so a one-line question to the innkeeper came back as
        // a paragraph of scene-setting with "what do you do next?" welded onto the end. The GM had
        // only two volumes — full scene or total silence — and defaulted to the loud one.
        sb.AppendLine("Match the length of your response to what the moment actually needs. Most turns are small: a single line of NPC dialogue, a one-sentence answer to what a character asks or examines, a brief description of what an action reveals, a short ruling. Reply with just that and stop — no scene-setting the players already have, no recap of what just happened, no restating the situation. One or two sentences is a complete, correct turn, and far more often right than a full paragraph. Reserve full, vivid narration for moments that earn it: a new place, a new arrival, combat opening or turning, a revelation, a scene changing shape.");
        sb.AppendLine("Only end with an open question or a call to action when the party genuinely faces a choice and needs prompting. After ordinary conversation or a minor action, simply stop — the players know it is their turn, and asking \"what do you do?\" after every exchange makes the table feel interrogated.");

        // Every in-character line a player sends fires a narrate call, so without an explicit
        // way to pass, the GM was structurally obliged to interject on every single one —
        // including the halves of a conversation the characters are having with each other.
        sb.AppendLine("Not every player line needs a response. When the characters are talking among themselves, planning, or bantering, and nothing in the world would react, call the \"wait\" tool as your only tool call and write no narration — the players simply keep talking. Answering a question put to an NPC, resolving an action against the world, or reacting to something that changes the scene is never a wait.");

        var silence = await CountTableMessagesSinceGMSpokeAsync(sessionId);
        if (silence >= WaitStreakLimit)
        {
            // Otherwise a model that has settled into waiting can keep waiting indefinitely,
            // and the table is left talking to itself with no GM at all.
            sb.AppendLine($"You have stayed silent for the last {silence} table messages. Do NOT call \"wait\" this turn — respond, move the scene, or bring something into it.");
        }

        return sb.ToString();
    }

    /// <summary>Number of in-character table messages since the GM last said anything to the
    /// whole table. GM output (narration, tool-driven dice results, loot) is written with no
    /// PlayerId and no whisper routing, so that is what marks the boundary.</summary>
    private const int WaitStreakLimit = 3;

    private async Task<int> CountTableMessagesSinceGMSpokeAsync(Guid sessionId)
    {
        var lastGMAt = await db.Messages
            .Where(m => m.SessionId == sessionId && m.PlayerId == null && m.WhisperToId == null && m.WhisperFromId == null)
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => (DateTimeOffset?)m.CreatedAt)
            .FirstOrDefaultAsync();

        return await db.Messages
            .Where(m => m.SessionId == sessionId && m.Type == "Chat"
                && (lastGMAt == null || m.CreatedAt > lastGMAt))
            .CountAsync();
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
