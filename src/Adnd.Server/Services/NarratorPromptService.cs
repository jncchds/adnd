using Adnd.Server.Data;
using Adnd.Server.Dtos;
using Adnd.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Services;

/// <summary>
/// Builds what the narrator is told at the start of a GM turn. It runs when the turn starts
/// (<c>AgentSaga.Start</c>), not when the player spoke: turns in one game run one at a time,
/// and a line that waited behind another must be answered with the fight, the cast and the
/// chat as the earlier turn left them.
/// </summary>
public interface INarratorPromptService
{
    /// <summary>Fills in the prompts of a call queued with <see cref="GMDispatchOptions.Pending"/> set.</summary>
    Task<GMDispatchOptions> BuildAsync(AgentCall call, GMDispatchOptions pending, CancellationToken ct = default);
}

public class NarratorPromptService(
    AppDbContext db,
    IRAGService rag,
    INPCRelevanceService npcRelevance,
    ICombatTracker combats,
    IStoryNudgeService nudges,
    ISessionManagementService sessions) : INarratorPromptService
{
    public async Task<GMDispatchOptions> BuildAsync(AgentCall call, GMDispatchOptions pending, CancellationToken ct = default)
    {
        var sessionId = call.SessionId ?? (await sessions.GetOrCreateCurrentSessionAsync(call.GameId)).Id;
        var opening = pending.Pending == GMDispatchOptions.PendingOpening;
        var systemPrompt = await BuildSystemPromptAsync(call.GameId, sessionId, pending.SpeakerPlayerId, opening, ct);

        // Without recent-message context each narrate call only ever saw the single line the
        // player just sent, with no memory of the last several exchanges — the GM re-invented
        // the scene from scratch on every turn instead of continuing it.
        var context = await rag.GeneratePlotContextAsync(call.GameId, ct);

        string action;
        if (opening)
        {
            var seed = await db.Games.Where(g => g.Id == call.GameId).Select(g => g.PlotSeed).FirstOrDefaultAsync(ct);
            action = "=== OPENING SCENE ===\n"
                + (string.IsNullOrEmpty(seed) ? "Begin the session." : $"Begin the session from this seed: {seed}")
                + " No player has spoken yet. Set the scene with full, vivid narration, establish the atmosphere, and give the player characters a reason to act. Do not call wait."
                + " If the scene opens in a fight, it exists only once you have called startCombat and addCombatParticipant — never announce a combat, an initiative order or anyone's hit points in prose alone.";
        }
        else
        {
            // The client sends the bare line; who said it was left for the model to guess.
            var speaker = pending.SpeakerPlayerId is { } speakerId
                ? await db.Players.Where(p => p.Id == speakerId).Select(p => p.CharacterName).FirstOrDefaultAsync(ct)
                : null;
            action = "=== CURRENT ACTION ===\n" + (string.IsNullOrWhiteSpace(speaker) ? pending.UserPrompt : $"{speaker}: {pending.UserPrompt}");

            // Said while the previous turn was still being resolved, and answered only now.
            var overtaken = await db.Messages.AnyAsync(m => m.SessionId == sessionId && m.PlayerId == null
                && m.WhisperToId == null && m.WhisperFromId == null && m.CreatedAt > call.CreatedAt, ct);
            if (overtaken)
                action += "\n(This was said while you were still resolving the action before it. The story above has moved on since; answer it as things stand now.)";
        }

        return pending with
        {
            SystemPrompt = systemPrompt,
            UserPrompt = string.IsNullOrEmpty(context) ? action : $"{context}\n\n{action}",
            Pending = null
        };
    }

    private async Task<string> BuildSystemPromptAsync(Guid gameId, Guid sessionId, Guid? speakerId, bool opening, CancellationToken ct)
    {
        var game = await db.Games.Include(g => g.Players).ThenInclude(p => p.User)
            .FirstOrDefaultAsync(g => g.Id == gameId, ct);
        if (game == null) return "You are the AI Game Master for an ongoing TTRPG session.";

        var plots = await db.PlotThreads
            .Where(p => p.GameId == gameId && !p.IsDeleted && p.Status == PlotThreadStatus.Active)
            .OrderByDescending(p => p.Momentum)
            .Take(4)
            .ToListAsync(ct);

        // Not every NPC in the campaign — only the ones the situation is about. See
        // NPCRelevanceService for what "relevant" means here.
        var npcs = await npcRelevance.GetRelevantAsync(gameId, sessionId, ct: ct);

        var characters = await db.Characters
            .Where(c => c.Player.GameId == gameId && !c.IsDeleted)
            .Include(c => c.Player)
            .ToListAsync(ct);

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
                if (CharacterStateService.IsDead(c))
                {
                    // Their player can only bring in someone new; nothing may be rolled or decided for the dead.
                    sb.AppendLine($"- {c.Name} ({c.Race} {c.Class}) — DEAD. They take no part in the story; never act, speak or roll for them.");
                    continue;
                }
                var conditions = CombatTracker.ReadConditions(c.Conditions);
                var conditionSuffix = conditions.Count > 0 ? $" — {string.Join(", ", conditions)}" : "";
                var weapons = string.Join("; ", ClassRules.Weapons(c.Inventory));
                var weaponSuffix = weapons.Length > 0 ? $" — weapons: {weapons}" : "";
                sb.AppendLine($"- {c.Name} (Level {c.Level} {c.Race} {c.Class}, HP {c.CurrentHP}/{c.MaxHP}, AC {c.ArmourClass}, proficiency +{c.ProficiencyBonus}){modsSuffix}{weaponSuffix}{conditionSuffix}");
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

        // Without this the narrator had no idea a fight was on, who stood where, or whose turn
        // it was — it could start a combat and then forget it existed on the next message.
        if (await combats.DescribeForNarratorAsync(gameId, speakerId, ct) is { } combatBlock)
        {
            sb.AppendLine(combatBlock);
        }
        else
        {
            sb.AppendLine("When a fight breaks out, call startCombat in the same response as your narration: name the encounter and lay out 2-5 zones in order (neighbours are adjacent — e.g. Riverbank, Bridge, Tower door), with the zone the party stands in. The player characters join automatically. Then call addCombatParticipant once per enemy, with a distinct name, hit points, AC and zone.");
        }

        sb.AppendLine("Narrate the scene vividly when the scene calls for it. You are the Game Master: the rules, the dice and the characters' sheets are yours to run, through your tools.");
        // The model answers, its tools run, and it is asked again with the results. It has to
        // be told so: models trained on one-shot tool use otherwise pack a roll and the damage
        // that depends on it into the same response.
        sb.AppendLine("You work in steps. Each time you call tools they are run, you are shown what they returned, and you continue from there — so never decide something a result has not told you yet. Roll first; in the next step, having seen the roll, apply what follows from it (the attack tool is the exception: it rolls and applies an attack in one call). Your turn ends when you reply with narration and no tool calls, and it should end only when the next move belongs to a player.");
        sb.AppendLine("The table reads the chat as you go. Never narrate first and make the tool call in a later reply. A roll — attack, requestPlayerRoll, rollDice, deathSave — goes without narration beside it: you do not know how it falls until its result comes back, and anything you write next to it is kept from the table. Give every roll a reason, which is what the table reads with the dice; then narrate the attempt and its outcome together once you have the result. Text you send beside other tool calls (starting a fight, handing over an item) is shown at once. Your closing narration only needs what has not been said.");
        sb.AppendLine("Players never roll dice themselves. Any roll a player character makes — a check, a save, a contest — goes through requestPlayerRoll, naming the character; rollDice is for NPCs, monsters and the world; an attack in a fight, whoever makes it, is the attack tool. When a roll is needed, use a fully-resolved dice formula such as \"1d20+3\" — add the relevant ability modifier listed above yourself. Never write a placeholder like \"1d20+{strength}\"; the dice engine cannot resolve it.");
        sb.AppendLine("A character's sheet changes only through your tools, never through narration alone: damage and heal for hit points, setCondition for conditions, giveItem and removeItem whenever something is gained, bought, spent, used up or lost (coins are the item \"Gold pieces\"), castSpell whenever a levelled spell is cast, rest once the fiction has given them a short or long rest, and awardExperience when the party overcomes a fight or a challenge. Use queryCharacter to check what a character carries or has left.");

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
        if (!opening)
            sb.AppendLine("Not every player line needs a response. When the characters are talking among themselves, planning, or bantering, and nothing in the world would react, call the \"wait\" tool as your only tool call and write no narration — the players simply keep talking. Answering a question put to an NPC, resolving an action against the world, or reacting to something that changes the scene is never a wait.");

        // Spends a turn from each limited nudge: this prompt is one narrator turn.
        if (nudges.Describe(await nudges.TakeForNarrationAsync(gameId, ct)) is { } direction)
        {
            sb.AppendLine();
            sb.AppendLine(direction);
        }

        var silence = opening ? 0 : await CountTableMessagesSinceGMSpokeAsync(sessionId, ct);
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

    private async Task<int> CountTableMessagesSinceGMSpokeAsync(Guid sessionId, CancellationToken ct)
    {
        var lastGMAt = await db.Messages
            .Where(m => m.SessionId == sessionId && m.PlayerId == null && m.WhisperToId == null && m.WhisperFromId == null)
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => (DateTimeOffset?)m.CreatedAt)
            .FirstOrDefaultAsync(ct);

        return await db.Messages
            .Where(m => m.SessionId == sessionId && m.Type == "Chat"
                && (lastGMAt == null || m.CreatedAt > lastGMAt))
            .CountAsync(ct);
    }
}
