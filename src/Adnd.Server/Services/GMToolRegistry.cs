using System.Text.Json;
using Adnd.Server.Data;
using Adnd.Server.Hubs;
using Adnd.Server.Models;
using Adnd.Server.Services.Llm;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using CombatEntity = Adnd.Server.Models.Combat;

namespace Adnd.Server.Services;

public interface IGMToolRegistry
{
    IEnumerable<ToolDefinition> GetToolDefinitions();
    bool RequiresConfirmation(string toolName);
    Task<string> ExecuteToolAsync(string toolName, JsonElement arguments, Guid gameId, Guid sessionId, CancellationToken ct);
}

public class GMToolRegistry(AppDbContext db, IDiceEngine diceEngine, IHubContext<GameHub> hub) : IGMToolRegistry
{
    private static JsonElement Schema(string json)
        => JsonSerializer.Deserialize<JsonElement>(json);

    /// <summary>
    /// Reads a GUID argument without throwing. Tool arguments are produced by an LLM, so
    /// a missing property or a non-GUID string is an expected input, not an exception —
    /// Guid.Parse used to blow up the whole tool call on either.
    /// </summary>
    private static bool TryGetGuid(JsonElement arguments, string property, out Guid value)
    {
        value = Guid.Empty;
        if (arguments.ValueKind != JsonValueKind.Object) return false;
        if (!arguments.TryGetProperty(property, out var element)) return false;

        return element.ValueKind switch
        {
            JsonValueKind.String => Guid.TryParse(element.GetString(), out value),
            _ => false
        };
    }

    public IEnumerable<ToolDefinition> GetToolDefinitions() =>
    [
        new("narrate", "Output narrative text to players",
            Schema("""{"type":"object","properties":{"text":{"type":"string"}},"required":["text"]}""")),
        new("rollDice", "Roll dice using a fully-resolved formula, e.g. \"1d20+3\". Never use a placeholder like \"{strength}\" — look up the character's ability modifier (from context or queryCharacter) and write the number in yourself. Include \"dc\" whenever the roll is against a target number — the result will state whether it succeeded, so you don't have to compare it yourself.",
            Schema("""{"type":"object","properties":{"formula":{"type":"string"},"reason":{"type":"string"},"dc":{"type":"integer"}},"required":["formula"]}""")),
        new("skillCheck", "Perform a skill check for a character",
            Schema("""{"type":"object","properties":{"characterId":{"type":"string"},"skillId":{"type":"string"},"dc":{"type":"integer"}},"required":["characterId","skillId","dc"]}""")),
        new("requestPlayerRoll", "Request a player to roll dice. formula must be fully-resolved, e.g. \"1d20+2\" — never a placeholder like \"{strength}\"; look up the character's ability modifier first. Include \"dc\" whenever the roll is against a target number — the result will state whether it succeeded.",
            Schema("""{"type":"object","properties":{"playerId":{"type":"string"},"formula":{"type":"string"},"reason":{"type":"string"},"dc":{"type":"integer"}},"required":["playerId","formula","reason"]}""")),
        new("queryCharacter", "Retrieve character data",
            Schema("""{"type":"object","properties":{"characterId":{"type":"string"}},"required":["characterId"]}""")),
        new("queryNPCs", "Retrieve NPC list for a game",
            Schema("""{"type":"object","properties":{"gameId":{"type":"string"}},"required":["gameId"]}""")),
        new("searchPlotContext", "Search plot context using RAG",
            Schema("""{"type":"object","properties":{"query":{"type":"string"}},"required":["query"]}""")),
        new("updateGameState", "Update the game state JSON",
            Schema("""{"type":"object","properties":{"state":{"type":"string"}},"required":["state"]}""")),
        new("sendWhisper", "Send a whisper message to a player",
            Schema("""{"type":"object","properties":{"targetPlayerId":{"type":"string"},"content":{"type":"string"}},"required":["targetPlayerId","content"]}""")),
        new("startCombat", "Start a new combat encounter",
            Schema("""{"type":"object","properties":{"name":{"type":"string"},"participantIds":{"type":"array","items":{"type":"string"}}},"required":["name"]}""")),
        new("addCombatParticipant", "Add a participant to combat",
            Schema("""{"type":"object","properties":{"combatId":{"type":"string"},"displayName":{"type":"string"},"hp":{"type":"integer"},"ac":{"type":"integer"},"initiative":{"type":"number"}},"required":["combatId","displayName","hp","ac"]}""")),
        new("generateLoot", "Generate loot for a combat encounter",
            Schema("""{"type":"object","properties":{"combatId":{"type":"string"},"defeatedNPCIds":{"type":"array","items":{"type":"string"}}},"required":["combatId","defeatedNPCIds"]}""")),
        new("wait", "Stay silent this turn and let the players keep talking. Use this — as the ONLY tool call, with no narration text — when the characters are conversing among themselves, deliberating, or otherwise doing something that needs no ruling, no roll and no reaction from the world. Players see nothing at all; the scene simply continues. Do not use it as a way to avoid a question the players put to you, an NPC, or the world.",
            Schema("""{"type":"object","properties":{"reason":{"type":"string"}}}""")),
    ];

    public bool RequiresConfirmation(string toolName) => toolName == "requestPlayerRoll";

    public async Task<string> ExecuteToolAsync(string toolName, JsonElement arguments, Guid gameId, Guid sessionId, CancellationToken ct)
    {
        switch (toolName)
        {
            case "narrate":
                return arguments.GetProperty("text").GetString() ?? string.Empty;

            // A wait-only response never reaches here — AgentSaga intercepts it at
            // LLMResponseReceived and completes the turn silently without executing anything.
            // This case exists for the contradictory response where the model asks to wait
            // *and* to do something else in the same turn: the other tools run normally and
            // the wait degrades to a no-op the follow-up call is told about, rather than a
            // "Unknown tool" string that reads to the model like a malfunction.
            case "wait":
                return "Acknowledged — no GM action was taken for this part of the turn.";

            case "rollDice":
            {
                var formula = arguments.GetProperty("formula").GetString() ?? "1d20";
                var result = diceEngine.Roll(formula);
                var (content, dc, success) = FormatRollContent(formula, result, arguments);
                await BroadcastRollAsync(gameId, sessionId, content, "DiceRoll",
                    new { formula = result.Formula, total = result.Total, breakdown = result.Breakdown, dc, success }, ct);
                // Previously returned only result.Breakdown — the follow-up narration call
                // never learned whether a rollDice-with-dc succeeded or failed, so it had to
                // guess. Returning the full content (which states Success/Failure) fixes that.
                return content;
            }

            case "skillCheck":
            {
                var roll = diceEngine.Roll("1d20");
                var dc = arguments.TryGetProperty("dc", out var dcEl) ? dcEl.GetInt32() : 10;
                var success = roll.Total >= dc;
                var content = $"Skill check vs DC {dc}: {roll.Breakdown} — {(success ? "Success" : "Failure")}";
                await BroadcastRollAsync(gameId, sessionId, content, "SkillCheck",
                    new { dc, total = roll.Total, success, breakdown = roll.Breakdown }, ct);
                return content;
            }

            case "requestPlayerRoll":
            {
                // Reached only after player approval (ToolExecutionHandler skips straight to
                // "Declined: ..." on decline without calling ExecuteToolAsync at all). The
                // confirm/decline endpoints don't collect a player-submitted roll value, so
                // approval means "roll it for me" — same as skillCheck/rollDice, just
                // attributed to the target player instead of the GM.
                var formula = arguments.GetProperty("formula").GetString() ?? "1d20";
                var reason = arguments.TryGetProperty("reason", out var reasonEl) ? reasonEl.GetString() : null;
                var roll = diceEngine.Roll(formula);
                TryGetGuid(arguments, "playerId", out var rollerId);
                var (content, dc, success) = FormatRollContent(formula, roll, arguments, reason);
                await BroadcastRollAsync(gameId, sessionId, content, "DiceRoll",
                    new { formula = roll.Formula, total = roll.Total, breakdown = roll.Breakdown, dc, success }, ct,
                    rollerId == Guid.Empty ? null : rollerId);
                return content;
            }

            case "queryCharacter":
            {
                // Ids here come from LLM-generated arguments, so they may be hallucinated,
                // malformed, or belong to a different game. Parse defensively and scope
                // the lookup to this game.
                if (!TryGetGuid(arguments, "characterId", out var charId))
                    return "Invalid characterId";

                var character = await db.Characters
                    .FirstOrDefaultAsync(c => c.Id == charId && c.Player.GameId == gameId, ct);
                if (character == null) return "Character not found";
                return JsonSerializer.Serialize(new
                {
                    character.Id,
                    character.Name,
                    character.Class,
                    character.Level,
                    character.CurrentHP,
                    character.MaxHP,
                    // Ability modifiers as text ("STR +2, DEX +0, …") so the LLM can drop a
                    // real number straight into a dice formula instead of guessing.
                    abilityModifiers = AbilityScoreHelper.FormatModifiers(character.Attributes)
                });
            }

            case "queryNPCs":
            {
                var npcs = await db.NPCs.Where(n => n.GameId == gameId).ToListAsync(ct);
                return JsonSerializer.Serialize(npcs.Select(n => new { n.Id, n.Name, n.Description, n.Attitude }));
            }

            case "searchPlotContext":
            {
                var query = arguments.GetProperty("query").GetString() ?? string.Empty;
                return $"Plot context search: {query} (RAG not yet implemented)";
            }

            case "updateGameState":
            {
                var state = arguments.GetProperty("state").GetString();
                var game = await db.Games.FindAsync([gameId], ct);
                if (game != null) { game.GameState = state; await db.SaveChangesAsync(ct); }
                return "Game state updated";
            }

            case "sendWhisper":
            {
                var content = arguments.GetProperty("content").GetString() ?? string.Empty;

                // targetPlayerId is declared required in this tool's schema but was never
                // read, so WhisperToId stayed null and the whisper was invisible to every
                // player — including the one it was meant for.
                if (!TryGetGuid(arguments, "targetPlayerId", out var targetPlayerId))
                    return "Invalid or missing targetPlayerId";

                var target = await db.Players
                    .FirstOrDefaultAsync(p => p.Id == targetPlayerId && p.GameId == gameId, ct);
                if (target == null) return "Target player not found in this game";

                var whisperMsg = new Message
                {
                    SessionId = sessionId,
                    Content = content,
                    Type = "Whisper",
                    WhisperToId = target.Id,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                db.Messages.Add(whisperMsg);
                await db.SaveChangesAsync(ct);

                // A whisper is private: broadcast to the target user only, never the game group
                // (mirrors GameHub.SendGMWhisper) — otherwise every player would see it.
                var whisperDto = new MessageDto(whisperMsg.Id, sessionId, null, content, "Whisper", false, whisperMsg.CreatedAt, null);
                await hub.Clients.User(target.UserId.ToString()).SendAsync("NewMessage", whisperDto, ct);
                return $"Whisper sent to {target.CharacterName}";
            }

            case "startCombat":
            {
                var name = arguments.GetProperty("name").GetString() ?? "Combat";
                var combat = new CombatEntity
                {
                    GameId = gameId,
                    SessionId = sessionId,
                    Name = name
                };
                db.Combats.Add(combat);
                await db.SaveChangesAsync(ct);
                return JsonSerializer.Serialize(new { combat.Id, combat.Name });
            }

            case "addCombatParticipant":
            {
                if (!TryGetGuid(arguments, "combatId", out var combatId))
                    return "Invalid combatId";

                // The combat must belong to this game, or the GM agent could write into
                // another table's encounter via a hallucinated id.
                if (!await db.Combats.AnyAsync(c => c.Id == combatId && c.GameId == gameId, ct))
                    return "Combat not found in this game";

                var displayName = arguments.GetProperty("displayName").GetString() ?? string.Empty;
                var hp = arguments.GetProperty("hp").GetInt32();
                var ac = arguments.GetProperty("ac").GetInt32();
                var initiative = arguments.TryGetProperty("initiative", out var initEl) ? initEl.GetSingle() : 0f;
                var participant = new CombatParticipant
                {
                    CombatId = combatId,
                    DisplayName = displayName,
                    HP = hp,
                    MaxHP = hp,
                    AC = ac,
                    Initiative = initiative
                };
                db.CombatParticipants.Add(participant);
                await db.SaveChangesAsync(ct);
                return JsonSerializer.Serialize(new { participant.Id, participant.DisplayName });
            }

            case "generateLoot":
            {
                if (!TryGetGuid(arguments, "combatId", out var lootCombatId))
                    return "Invalid combatId";

                if (!await db.Combats.AnyAsync(c => c.Id == lootCombatId && c.GameId == gameId, ct))
                    return "Combat not found in this game";

                var defeatedIds = new List<Guid>();
                if (arguments.TryGetProperty("defeatedNPCIds", out var idsEl) && idsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var idEl in idsEl.EnumerateArray())
                        if (idEl.ValueKind == JsonValueKind.String && Guid.TryParse(idEl.GetString(), out var npcId))
                            defeatedIds.Add(npcId);
                }
                if (defeatedIds.Count == 0)
                    return "No valid defeatedNPCIds provided";

                // Ids are LLM-generated — only loot NPCs that actually fought in this combat,
                // or a hallucinated id could pull loot from an unrelated NPC in the game.
                var participantNpcIds = await db.CombatParticipants
                    .Where(p => p.CombatId == lootCombatId && p.NPCId != null && defeatedIds.Contains(p.NPCId.Value))
                    .Select(p => p.NPCId!.Value)
                    .ToListAsync(ct);

                var npcs = await db.NPCs
                    .Where(n => n.GameId == gameId && participantNpcIds.Contains(n.Id))
                    .ToListAsync(ct);
                if (npcs.Count == 0)
                    return "None of the given NPCs were found in that combat";

                // No item-claiming flow exists yet (ICombatInventoryService only consumes
                // items, it has no AddItemAsync), so loot is generated and narrated but not
                // auto-transferred into any player's Character.Inventory.
                var entries = new List<object>();
                var summaryLines = new List<string>();
                var totalGold = 0;
                foreach (var npc in npcs)
                {
                    var items = new List<string>();
                    if (npc.Inventory.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var prop in npc.Inventory.EnumerateObject())
                        {
                            var name = prop.Value.ValueKind == JsonValueKind.Object && prop.Value.TryGetProperty("name", out var n)
                                ? n.GetString() : prop.Name;
                            items.Add(name ?? prop.Name);
                        }
                    }
                    else if (npc.Inventory.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var el in npc.Inventory.EnumerateArray())
                            if (el.ValueKind == JsonValueKind.String) items.Add(el.GetString() ?? "");
                    }

                    var gold = diceEngine.Roll("2d10").Total;
                    totalGold += gold;
                    entries.Add(new { npc.Id, npc.Name, items, gold });
                    summaryLines.Add($"{npc.Name}: {(items.Count > 0 ? string.Join(", ", items) : "no items")}, {gold} gold");
                }

                var content = $"Loot recovered: {string.Join(" | ", summaryLines)} (total {totalGold} gold)";
                await BroadcastRollAsync(gameId, sessionId, content, "Loot", new { entries, totalGold }, ct);
                return JsonSerializer.Serialize(new { entries, totalGold });
            }

            default:
                throw new InvalidOperationException($"Unknown tool: {toolName}");
        }
    }

    /// <summary>
    /// Shared by rollDice/requestPlayerRoll. Without a stated target, a roll like "Attempting
    /// to force open the door: 17" leaves the player unable to tell if that succeeded — the
    /// GM knew the DC internally but nothing surfaced it, so success/failure had to be
    /// inferred (often wrongly) from the follow-up narration's prose.
    /// </summary>
    private static (string Content, int? Dc, bool? Success) FormatRollContent(
        string formula, DiceResult result, JsonElement arguments, string? reason = null)
    {
        int? dc = arguments.TryGetProperty("dc", out var dcEl) && dcEl.ValueKind == JsonValueKind.Number
            ? dcEl.GetInt32()
            : null;
        bool? success = dc.HasValue ? result.Total >= dc.Value : null;

        var prefix = string.IsNullOrEmpty(reason) ? "" : $"{reason} — ";
        var suffix = dc.HasValue
            ? $" vs DC {dc}: {result.Breakdown} — {(success!.Value ? "Success" : "Failure")}"
            : $": {result.Breakdown}";
        return ($"{prefix}Rolled {formula}{suffix}", dc, success);
    }

    // Mirrors GameHub.RollDice: GM-tool rolls must land their own chat message immediately,
    // not rely on the follow-up narrator LLM call to happen to mention the numbers — that call
    // is prose-only and routinely narrates around the roll without ever stating it.
    private async Task BroadcastRollAsync(Guid gameId, Guid sessionId, string content, string type, object metadata, CancellationToken ct, Guid? playerId = null)
    {
        var msg = new Message
        {
            SessionId = sessionId,
            PlayerId = playerId,
            Content = content,
            Type = type,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Messages.Add(msg);
        await db.SaveChangesAsync(ct);

        var dto = new MessageDto(msg.Id, sessionId, playerId, content, type, false, msg.CreatedAt, metadata);
        await hub.Clients.Group(gameId.ToString()).SendAsync("NewMessage", dto, ct);
    }
}
