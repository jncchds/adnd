using System.Text.Json;
using Adnd.Server.Data;
using Adnd.Server.Models;
using Adnd.Server.Services.Llm;
using Microsoft.EntityFrameworkCore;
using CombatEntity = Adnd.Server.Models.Combat;

namespace Adnd.Server.Services;

public interface IGMToolRegistry
{
    IEnumerable<ToolDefinition> GetToolDefinitions();
    bool RequiresConfirmation(string toolName);
    Task<string> ExecuteToolAsync(string toolName, JsonElement arguments, Guid gameId, Guid sessionId, CancellationToken ct);
}

public class GMToolRegistry(AppDbContext db, IDiceEngine diceEngine) : IGMToolRegistry
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
        new("rollDice", "Roll dice using a formula",
            Schema("""{"type":"object","properties":{"formula":{"type":"string"},"reason":{"type":"string"}},"required":["formula"]}""")),
        new("skillCheck", "Perform a skill check for a character",
            Schema("""{"type":"object","properties":{"characterId":{"type":"string"},"skillId":{"type":"string"},"dc":{"type":"integer"}},"required":["characterId","skillId","dc"]}""")),
        new("requestPlayerRoll", "Request a player to roll dice",
            Schema("""{"type":"object","properties":{"playerId":{"type":"string"},"formula":{"type":"string"},"reason":{"type":"string"}},"required":["playerId","formula","reason"]}""")),
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
    ];

    public bool RequiresConfirmation(string toolName) => toolName == "requestPlayerRoll";

    public async Task<string> ExecuteToolAsync(string toolName, JsonElement arguments, Guid gameId, Guid sessionId, CancellationToken ct)
    {
        switch (toolName)
        {
            case "narrate":
                return arguments.GetProperty("text").GetString() ?? string.Empty;

            case "rollDice":
            {
                var formula = arguments.GetProperty("formula").GetString() ?? "1d20";
                var result = diceEngine.Roll(formula);
                return result.Breakdown;
            }

            case "skillCheck":
            {
                var roll = diceEngine.Roll("1d20");
                var dc = arguments.TryGetProperty("dc", out var dcEl) ? dcEl.GetInt32() : 10;
                var success = roll.Total >= dc;
                return $"Skill check: rolled {roll.Total} vs DC {dc} — {(success ? "Success" : "Failure")}. {roll.Breakdown}";
            }

            case "requestPlayerRoll":
                return "Awaiting player roll confirmation";

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
                    character.MaxHP
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

                db.Messages.Add(new Message
                {
                    SessionId = sessionId,
                    Content = content,
                    Type = "Whisper",
                    WhisperToId = target.Id,
                    CreatedAt = DateTimeOffset.UtcNow
                });
                await db.SaveChangesAsync(ct);
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
                return "Loot generated for combat";

            default:
                throw new InvalidOperationException($"Unknown tool: {toolName}");
        }
    }
}
