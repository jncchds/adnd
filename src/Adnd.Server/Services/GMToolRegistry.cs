using System.Text.Json;
using Adnd.Server.Data;
using Adnd.Server.Hubs;
using Adnd.Server.Models;
using Adnd.Server.Services.Llm;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using CombatEntity = Adnd.Server.Models.Combat;

namespace Adnd.Server.Services;

/// <summary>A player roll ready to make, or why it can't be made — the error goes back to the
/// model as the tool result so it can correct the call.</summary>
/// <param name="Direct">The tool turned out not to be a player's roll after all — an
/// <c>attack</c> by an enemy — and is simply executed.</param>
public record PlayerRollPlan(PlayerRollRequest? Request, string? Error, bool Direct = false);

public interface IGMToolRegistry
{
    IEnumerable<ToolDefinition> GetToolDefinitions();
    bool RequiresConfirmation(string toolName, JsonElement arguments);

    /// <summary>Tools that roll on a player's behalf. ToolExecutionHandler drives these itself,
    /// because a reroll offer has to hold the GM's turn open, which a string result cannot do.</summary>
    bool IsPlayerRoll(string toolName);

    /// <summary>Resolves a player-roll tool's arguments — the character is named, not an id —
    /// into the roll to make. Called again at every stage of the roll, so every stage rolls the
    /// same thing.</summary>
    Task<PlayerRollPlan> PlanPlayerRollAsync(string toolName, JsonElement arguments, Guid gameId, Guid sessionId, CancellationToken ct);

    /// <summary>Applies the final number of a player roll, once any reroll is settled, and
    /// returns what the model is told.</summary>
    Task<string> FinishPlayerRollAsync(string toolName, JsonElement arguments, Guid gameId, int total, int? naturalD20, string content, CancellationToken ct);

    Task<string> ExecuteToolAsync(string toolName, JsonElement arguments, Guid gameId, Guid sessionId, CancellationToken ct);
}

public class GMToolRegistry(
    AppDbContext db,
    IDiceEngine diceEngine,
    IHubContext<GameHub> hub,
    ICombatTracker combats,
    ICharacterStateService characters) : IGMToolRegistry
{
    private const string RequestPlayerRoll = "requestPlayerRoll";
    private const string DeathSave = "deathSave";
    private const string Attack = "attack";

    // attack is one tool for everyone, so the model has a single way to resolve a blow; it
    // is a player's roll only when the attacker is a player character, which the plan decides.
    public bool IsPlayerRoll(string toolName) => toolName is RequestPlayerRoll or DeathSave or Attack;

    public async Task<PlayerRollPlan> PlanPlayerRollAsync(string toolName, JsonElement arguments, Guid gameId, Guid sessionId, CancellationToken ct)
    {
        if (toolName == DeathSave)
        {
            if (ReadOptionalString(arguments, "character") is not { } dyingName)
                return new(null, "Missing character");
            if (await combats.GetActiveAsync(gameId, ct) is not { } combat)
                return new(null, "No combat is in progress — death saves are made on a dying character's turn in combat.");
            if (combats.FindByName(combat, dyingName) is not { } p || p.PlayerId is null)
                return new(null, $"No player character called \"{dyingName}\" in this combat.");
            if (!combats.IsDying(p))
                return new(null, $"{p.DisplayName} is not dying, so makes no death save.");
            // One save a turn, on their own turn: asked again while the turn stood still, the
            // model rolled a second one.
            if (combats.Acting(combat) is { } acting && acting != p)
                return new(null, $"{p.DisplayName} makes a death save only on their own turn; it is {acting.DisplayName}'s turn.");

            // DC 10 so the roll states success or failure — and so an ability that rerolls a
            // failed check is offered on a failed death save, as it would be at a table.
            return new(new PlayerRollRequest(gameId, sessionId, p.PlayerId, "1d20", 10, "Death saving throw"), null);
        }

        if (toolName == Attack)
        {
            if (await combats.GetActiveAsync(gameId, ct) is not { } fight)
                return new(null, "No combat is in progress. Start one with startCombat.");
            var (attack, problem) = ReadAttack(fight, arguments);
            if (attack is null) return new(null, problem);
            if (attack.Attacker.ParticipantType != CombatParticipantType.Character) return new(null, null, Direct: true);

            // The target's AC is the DC, so the roll states hit or miss and an ability that
            // rerolls a failed check is offered on a miss.
            return new(new PlayerRollRequest(gameId, sessionId, attack.Attacker.PlayerId, $"1d20{attack.Bonus:+0;-0}", attack.Target.AC,
                $"{attack.Attacker.DisplayName} attacks {attack.Target.DisplayName}"), null);
        }

        var formula = ReadOptionalString(arguments, "formula") ?? "1d20";
        var reason = ReadOptionalString(arguments, "reason");

        // By name, as everything else the model addresses. It was only ever given names, so the
        // id this tool used to require was invented on every call — and an invented id rolled for
        // nobody: no reroll offer, and an optional roll asked of no one stalled the turn.
        Guid? playerId = null;
        if (ReadOptionalString(arguments, "character") is { } name)
        {
            var character = await characters.FindAsync(gameId, name, ct);
            if (character is null)
                return new(null, $"No player character called \"{name}\". The party: {await characters.PartyNamesAsync(gameId, ct)}.");
            if (CharacterStateService.IsDead(character))
                return new(null, $"{character.Name} is dead and makes no rolls.");
            playerId = character.PlayerId;
        }
        else if (TryGetGuid(arguments, "playerId", out var legacyId)
                 && await db.Players.AnyAsync(p => p.Id == legacyId && p.GameId == gameId, ct))
        {
            playerId = legacyId;
        }

        if (playerId is null)
            return new(null, $"Name the character who rolls in \"character\". The party: {await characters.PartyNamesAsync(gameId, ct)}.");

        return new(new PlayerRollRequest(gameId, sessionId, playerId, formula, ReadDc(arguments), reason), null);
    }

    public async Task<string> FinishPlayerRollAsync(string toolName, JsonElement arguments, Guid gameId, int total, int? naturalD20, string content, CancellationToken ct)
    {
        if (toolName == Attack)
            return await ActiveCombatToolAsync(gameId, ct, async combat =>
            {
                // Read again: the player may have taken a while over a reroll.
                var (attack, problem) = ReadAttack(combat, arguments);
                return attack is null
                    ? $"{content}\n{problem}"
                    : $"{content}\n{await LandAttackAsync(combat, attack, null, total, naturalD20, ct)}";
            });

        if (toolName != DeathSave) return content;

        return await ActiveCombatToolAsync(gameId, ct, async combat =>
        {
            var name = ReadOptionalString(arguments, "character") ?? "";
            if (combats.FindByName(combat, name) is not { } p) return $"{content}\n{name} is no longer in the combat.";
            var outcome = await combats.RecordDeathSaveAsync(combat, p.Id, total, ct);
            if (p.HP > 0)
                return $"{content}\n{outcome}\n{p.DisplayName} is conscious and it is still their turn: the next move is their player's.";

            // Told in so many words: after "they die" in the log, the narration still had the
            // character breathing shallowly and asked their player what they do.
            if (combats.IsDead(p))
                outcome += $"\n{p.DisplayName} is dead — not unconscious, not dying. Say so plainly; they do not breathe, speak or act again, and their player is not asked what they do.";
            if (combats.FightOver(combat) is { } over) return $"{content}\n{outcome}\n{over}";

            // The save is the dying character's whole turn, so the turn moves on with it.
            var next = await combats.AdvanceTurnAsync(combat, ct);
            return $"{content}\n{outcome}\n{p.DisplayName}'s turn is over." + (next is null ? "" : $" {DescribeTurn(combat, next)}");
        });
    }

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

    /// <summary>
    /// Optional string argument, normalised to null when absent, non-string or blank, so a
    /// caller can tell "the model didn't supply this" from "the model supplied a value".
    /// </summary>
    private static string? ReadOptionalString(JsonElement arguments, string property)
    {
        if (arguments.ValueKind != JsonValueKind.Object) return null;
        if (!arguments.TryGetProperty(property, out var element)) return null;
        if (element.ValueKind != JsonValueKind.String) return null;

        var value = element.GetString()?.Trim();
        return string.IsNullOrEmpty(value) ? null : value;
    }

    private static bool TryGetInt(JsonElement arguments, string property, out int value)
    {
        value = 0;
        return arguments.ValueKind == JsonValueKind.Object
            && arguments.TryGetProperty(property, out var element)
            && element.ValueKind == JsonValueKind.Number
            && element.TryGetInt32(out value);
    }

    /// <summary>Zones as the model writes them: objects with a name, or bare strings.</summary>
    private static List<ZoneSpec> ReadZones(JsonElement arguments)
    {
        var zones = new List<ZoneSpec>();
        if (arguments.ValueKind != JsonValueKind.Object
            || !arguments.TryGetProperty("zones", out var el)
            || el.ValueKind != JsonValueKind.Array)
            return zones;

        foreach (var z in el.EnumerateArray())
        {
            if (z.ValueKind == JsonValueKind.String && z.GetString() is { } bare)
            {
                zones.Add(new ZoneSpec(bare, []));
            }
            else if (ReadOptionalString(z, "name") is { } name)
            {
                var tags = z.TryGetProperty("tags", out var t) && t.ValueKind == JsonValueKind.Array
                    ? t.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).ToList()
                    : [];
                zones.Add(new ZoneSpec(name, tags));
            }
        }
        return zones;
    }

    /// <summary>A broken combat rule goes back to the model as the tool result, so it can correct itself.</summary>
    private static async Task<string> CombatToolAsync(Func<Task<string>> action)
    {
        try { return await action(); }
        catch (CombatRuleException ex) { return TurnLog.Refused + ex.Message; }
    }

    private Task<string> ActiveCombatToolAsync(Guid gameId, CancellationToken ct, Func<CombatEntity, Task<string>> action)
        => CombatToolAsync(async () => await combats.GetActiveAsync(gameId, ct) is { } combat
            ? await action(combat)
            : TurnLog.Refused + "No combat is in progress. Start one with startCombat.");

    public IEnumerable<ToolDefinition> GetToolDefinitions() =>
    [
        new("narrate", "Say something to the table right now, before the tool calls that follow it in the same response run — use it to describe an action ahead of the roll that resolves it. Your plain reply text does the same; do not send the same words both ways.",
            Schema("""{"type":"object","properties":{"text":{"type":"string"}},"required":["text"]}""")),
        new("rollDice", "Roll dice using a fully-resolved formula, e.g. \"1d20+3\". Never use a placeholder like \"{strength}\" — look up the character's ability modifier (from context or queryCharacter) and write the number in yourself. Include \"dc\" whenever the roll is against a target number — the result will state whether it succeeded, so you don't have to compare it yourself.",
            Schema("""{"type":"object","properties":{"formula":{"type":"string"},"reason":{"type":"string"},"dc":{"type":"integer"}},"required":["formula"]}""")),
        new("requestPlayerRoll", "Roll for a player character — every roll a player character makes goes through this (checks, saves, contests, damage from a spell or effect), except an attack in a fight, which is the attack tool; players never roll themselves. Name the character. formula must be fully-resolved, e.g. \"1d20+2\" — never a placeholder like \"{strength}\"; add the character's modifier yourself. Include \"dc\" whenever the roll is against a target number — the result will state whether it succeeded. Set \"mandatory\": true when the rules give the character no choice about rolling — a saving throw, initiative, a check forced on them; it is rolled at once and the player is told the result. Leave it false (the default) when the character is choosing to attempt something and could decline — the player is asked first. Either way, if the character has an ability that can reroll the result, the player is offered it before you are told the final number.",
            Schema("""{"type":"object","properties":{"character":{"type":"string","description":"The player character's name"},"formula":{"type":"string"},"reason":{"type":"string"},"dc":{"type":"integer"},"mandatory":{"type":"boolean"}},"required":["character","formula","reason"]}""")),
        new("deathSave", "Make a dying player character's death saving throw. Call it when a player character at 0 hit points reaches their turn in combat. It is rolled for them and recorded: three successes stabilise, three failures kill, a natural 20 brings them back with 1 hit point.",
            Schema("""{"type":"object","properties":{"character":{"type":"string"}},"required":["character"]}""")),
        new("queryCharacter", "Look up a player character's full sheet: hit points, ability modifiers, conditions, inventory, spell slots, abilities, level and experience.",
            Schema("""{"type":"object","properties":{"name":{"type":"string"}},"required":["name"]}""")),
        new("queryNPCs", "List every NPC in the campaign, including dead and departed ones.",
            Schema("""{"type":"object","properties":{}}""")),
        new("registerNPC", "Record an NPC in the campaign roster so it persists between turns. Call this in the SAME response as \"narrate\" whenever your narration names or introduces an NPC who is not already listed as a known NPC, and again whenever an established NPC's attitude, faction or description changes. Matching is by name: registering a name that already exists updates that NPC instead of creating a duplicate, so it is always safe to call.",
            Schema("""{"type":"object","properties":{"name":{"type":"string"},"description":{"type":"string"},"attitude":{"type":"string","enum":["Friendly","Neutral","Unfriendly","Hostile"]},"faction":{"type":"string"}},"required":["name"]}""")),
        new("updateNPCStatus", "Write an NPC out of the living story. Call this in the SAME response as \"narrate\" when a registered NPC dies (\"Dead\"), or when they leave the story for good — they move away, are written out, or the plot has simply passed them by (\"Departed\"). Use \"Active\" to bring someone back. Departed and dead NPCs stay on record and can still be asked about with queryNPCs, but they stop being offered to you as part of the current cast, which is what keeps the known-NPC list short enough to be useful.",
            Schema("""{"type":"object","properties":{"name":{"type":"string"},"status":{"type":"string","enum":["Active","Dead","Departed"]},"reason":{"type":"string"}},"required":["name","status"]}""")),
        new("searchPlotContext", "Search the campaign's history — everything said at the table, plot threads and NPCs — for a word or phrase, e.g. a place or a name that came up sessions ago.",
            Schema("""{"type":"object","properties":{"query":{"type":"string"}},"required":["query"]}""")),
        new("updateGameState", "Update the game state JSON",
            Schema("""{"type":"object","properties":{"state":{"type":"string"}},"required":["state"]}""")),
        new("sendWhisper", "Tell one player something only their character perceives or knows. Name the character.",
            Schema("""{"type":"object","properties":{"character":{"type":"string"},"content":{"type":"string"}},"required":["character","content"]}""")),
        new("attack", "Resolve one attack in the fight, whoever makes it — an enemy or a player character — in a single call: it rolls to hit against the target's AC and, on a hit, rolls the damage and applies it. It then ends the attacker's turn, so an enemy's whole turn is one call; set endTurn false if the attacker has more to do this turn. Only the combatant whose turn it is can attack, unless it is a reaction. For a player character the roll is made on their behalf and any reroll they have is offered to their player before you are told the result.",
            Schema("""{"type":"object","properties":{"attacker":{"type":"string"},"target":{"type":"string"},"bonus":{"type":"integer","description":"Attack bonus added to the d20, e.g. 4. For a player character: the ability modifier plus their proficiency bonus."},"damage":{"type":"string","description":"Fully-resolved damage formula, e.g. 1d6+2"},"type":{"type":"string","description":"e.g. slashing, piercing"},"moveTo":{"type":"string","description":"Zone the attacker moves into before attacking, if they are not already in reach"},"endTurn":{"type":"boolean","description":"Defaults to true. False when the attacker still has something to do this turn."},"reaction":{"type":"boolean","description":"True for an attack made outside the attacker's own turn, e.g. an opportunity attack. It never ends a turn."},"evenIfDown":{"type":"boolean","description":"True to deliberately strike a player character who is already down and dying"}},"required":["attacker","target","bonus","damage"]}""")),
        new("damage", "Take hit points from a combatant or a player character for anything that is not an attack resolved by the attack tool — a spell, a fall, a trap, a blow struck outside a fight — after the roll has decided it lands. Works in and out of combat.",
            Schema("""{"type":"object","properties":{"target":{"type":"string","description":"Combatant or player character name"},"amount":{"type":"integer"},"type":{"type":"string","description":"e.g. slashing, fire"}},"required":["target","amount"]}""")),
        new("heal", "Restore hit points to a combatant or a player character. Healing does nothing for the dead: only when the story has truly brought a dead player character back — magic that raises the dead — set revive true.",
            Schema("""{"type":"object","properties":{"target":{"type":"string"},"amount":{"type":"integer"},"revive":{"type":"boolean","description":"True only when a dead player character is being raised from death"}},"required":["target","amount"]}""")),
        new("setCondition", "Apply or remove a condition (Poisoned, Prone, Frightened, Grappled, Restrained, Stunned, Unconscious, Blinded, Charmed, Invisible, …) on a combatant or player character.",
            Schema("""{"type":"object","properties":{"target":{"type":"string"},"condition":{"type":"string"},"applied":{"type":"boolean","description":"false to remove; defaults to true"}},"required":["target","condition"]}""")),
        new("giveItem", "Put an item into a player character's inventory — loot they take, something bought or handed to them, coins (use the item \"Gold pieces\").",
            Schema("""{"type":"object","properties":{"character":{"type":"string"},"item":{"type":"string"},"quantity":{"type":"integer","description":"Defaults to 1"}},"required":["character","item"]}""")),
        new("removeItem", "Take an item out of a player character's inventory — used up, sold, lost, given away, or coins spent.",
            Schema("""{"type":"object","properties":{"character":{"type":"string"},"item":{"type":"string"},"quantity":{"type":"integer","description":"Defaults to 1"}},"required":["character","item"]}""")),
        new("castSpell", "Spend a spell slot when a player character casts a levelled spell (not a cantrip). If no slot of that level is left, a higher one is spent; if none at all, the spell fails and you are told so.",
            Schema("""{"type":"object","properties":{"character":{"type":"string"},"spell":{"type":"string"},"level":{"type":"integer","description":"The spell's level, 1-9"}},"required":["character","spell","level"]}""")),
        new("rest", "The party (or the named characters) finishes a rest. A long rest restores hit points, spell slots and abilities; a short rest restores abilities that recharge on one. Only call it once the fiction has actually given them the time.",
            Schema("""{"type":"object","properties":{"type":{"type":"string","enum":["short","long"]},"characters":{"type":"array","items":{"type":"string"},"description":"Omit for the whole party"}},"required":["type"]}""")),
        new("awardExperience", "Award experience points for overcoming a challenge — a fight won, a problem solved, a milestone reached. Each character named (or the whole party) receives the full amount; crossing a threshold levels them up automatically.",
            Schema("""{"type":"object","properties":{"amount":{"type":"integer"},"reason":{"type":"string"},"characters":{"type":"array","items":{"type":"string"},"description":"Omit for the whole party"}},"required":["amount","reason"]}""")),
        new("startCombat", "Start a combat encounter. The player characters join automatically with initiative rolled; add each enemy with addCombatParticipant in the same response. Position is by zone, not by square: lay out 2-5 named zones in order, neighbours being adjacent.",
            Schema("""{"type":"object","properties":{"name":{"type":"string"},"zones":{"type":"array","description":"In order; neighbours are adjacent.","items":{"type":"object","properties":{"name":{"type":"string"},"tags":{"type":"array","items":{"type":"string"},"description":"Terrain the narrator rules on, e.g. difficult, cover, elevated"}},"required":["name"]}},"partyZone":{"type":"string","description":"Zone the party starts in; defaults to the first"}},"required":["name","zones"]}""")),
        new("addCombatParticipant", "Add an enemy or other combatant to the combat in progress. Give each a distinct name (\"Goblin 1\", \"Goblin 2\"); a name matching a registered NPC links them.",
            Schema("""{"type":"object","properties":{"name":{"type":"string"},"hp":{"type":"integer"},"ac":{"type":"integer"},"zone":{"type":"string"},"initiative":{"type":"number","description":"Omit to roll it"}},"required":["name","hp","ac"]}""")),
        new("moveParticipant", "Move a combatant to another zone of the combat in progress. Call it whenever narration has someone change zone.",
            Schema("""{"type":"object","properties":{"name":{"type":"string"},"zone":{"type":"string"}},"required":["name","zone"]}""")),
        new("setZones", "Redraw the zones of the combat in progress, when the battlefield changes shape (a bridge collapses, the fight spills into the street). Anyone in a zone that no longer exists is moved to the first zone.",
            Schema("""{"type":"object","properties":{"zones":{"type":"array","description":"In order; neighbours are adjacent.","items":{"type":"object","properties":{"name":{"type":"string"},"tags":{"type":"array","items":{"type":"string"},"description":"Terrain the narrator rules on, e.g. difficult, cover, elevated"}},"required":["name"]}}},"required":["zones"]}""")),
        new("nextTurn", "End the acting combatant's turn and pass to the next in initiative order. An attack already ends the attacker's turn; call this for a turn that ended any other way — a move, a spell, a player character who did something else.",
            Schema("""{"type":"object","properties":{}}""")),
        new("endCombat", "End the combat in progress — one side defeated, fled or surrendered.",
            Schema("""{"type":"object","properties":{"outcome":{"type":"string","description":"One line on how it ended"}}}""")),
        new("generateLoot", "Generate loot from defeated enemies that are registered NPCs. Defaults to the most recent combat and everyone defeated in it.",
            Schema("""{"type":"object","properties":{"combatId":{"type":"string"},"defeatedNPCIds":{"type":"array","items":{"type":"string"}}}}""")),
        new("wait", "Stay silent this turn and let the players keep talking. Use this — as the ONLY tool call, with no narration text — when the characters are conversing among themselves, deliberating, or otherwise doing something that needs no ruling, no roll and no reaction from the world. Players see nothing at all; the scene simply continues. Do not use it as a way to avoid a question the players put to you, an NPC, or the world.",
            Schema("""{"type":"object","properties":{"reason":{"type":"string"}}}""")),
    ];

    /// <summary>
    /// Asking a player to confirm a roll they have no choice about is pure friction: the
    /// confirm endpoint doesn't collect a rolled value either way, so on a saving throw the
    /// prompt only ever meant "may I apply the rules to you?". A mandatory roll therefore
    /// resolves immediately. The player still gets a say in the one place they actually have
    /// one — the reroll offer afterwards, if they have an ability that grants it.
    /// </summary>
    public bool RequiresConfirmation(string toolName, JsonElement arguments)
        => toolName == "requestPlayerRoll" && !IsMandatory(arguments);

    private static bool IsMandatory(JsonElement arguments)
        => arguments.ValueKind == JsonValueKind.Object
           && arguments.TryGetProperty("mandatory", out var el)
           && el.ValueKind == JsonValueKind.True;

    public async Task<string> ExecuteToolAsync(string toolName, JsonElement arguments, Guid gameId, Guid sessionId, CancellationToken ct)
    {
        switch (toolName)
        {
            case "narrate":
            {
                // It used to hand the text back to the model and show the table nothing.
                if (ReadOptionalString(arguments, "text") is not { } narration) return "Missing text";
                await BroadcastRollAsync(gameId, sessionId, narration, "GM", null, ct);
                return narration;
            }

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
                // The reason was accepted and dropped, so the table read "Rolled 1d20+5 vs DC 11"
                // with nothing to say whose roll it was or what for.
                var (content, dc, success) = FormatRollContent(formula, result, arguments, ReadOptionalString(arguments, "reason"));
                await BroadcastRollAsync(gameId, sessionId, content, "DiceRoll",
                    new { formula = result.Formula, total = result.Total, breakdown = result.Breakdown, dc, success }, ct);
                // Previously returned only result.Breakdown — the follow-up narration call
                // never learned whether a rollDice-with-dc succeeded or failed, so it had to
                // guess. Returning the full content (which states Success/Failure) fixes that.
                return content;
            }

            // ToolExecutionHandler drives these itself — see IsPlayerRoll.
            case RequestPlayerRoll:
            case DeathSave:
                throw new InvalidOperationException($"{toolName} is resolved by ToolExecutionHandler, not executed directly.");

            case "queryCharacter":
            {
                var name = ReadOptionalString(arguments, "name") ?? ReadOptionalString(arguments, "character");
                if (name is null) return "Missing name";
                var character = await characters.FindAsync(gameId, name, ct);
                if (character == null)
                    return $"No player character called \"{name}\". The party: {await characters.PartyNamesAsync(gameId, ct)}.";
                return JsonSerializer.Serialize(new
                {
                    character.Name,
                    character.Race,
                    character.Class,
                    character.Level,
                    character.Experience,
                    nextLevelAt = ClassRules.XpForNextLevel(character.Level),
                    character.CurrentHP,
                    character.MaxHP,
                    character.ProficiencyBonus,
                    // Ability modifiers as text ("STR +2, DEX +0, …") so the LLM can drop a
                    // real number straight into a dice formula instead of guessing.
                    abilityModifiers = AbilityScoreHelper.FormatModifiers(character.Attributes),
                    conditions = CombatTracker.ReadConditions(character.Conditions),
                    inventory = ClassRules.ReadInventory(character.Inventory).Select(i => i.Quantity == 1 ? i.Name : $"{i.Name} ×{i.Quantity}"),
                    spellSlots = ClassRules.RefitSlots(character.SpellSlots, character.Class, character.Level)
                        .ToDictionary(kv => $"level {kv.Key}", kv => $"{kv.Value.Remaining}/{kv.Value.Total}"),
                    abilities = FeatureNames(character.Features),
                });
            }

            case "queryNPCs":
            {
                // Unlike the roster in the system prompt, this is deliberately everyone —
                // it is the escape hatch for "who was that innkeeper three towns back",
                // so dead and departed NPCs are included and labelled as such.
                var npcs = await db.NPCs.Where(n => n.GameId == gameId).ToListAsync(ct);
                return JsonSerializer.Serialize(npcs.Select(n => new
                {
                    n.Id,
                    n.Name,
                    n.Description,
                    Attitude = n.Attitude.ToString(),
                    Status = n.Status.ToString(),
                    n.Faction
                }));
            }

            case "registerNPC":
            {
                var name = arguments.TryGetProperty("name", out var nameEl) && nameEl.ValueKind == JsonValueKind.String
                    ? nameEl.GetString()?.Trim()
                    : null;
                if (string.IsNullOrWhiteSpace(name))
                    return "Invalid or missing name";

                var description = ReadOptionalString(arguments, "description");
                var faction = ReadOptionalString(arguments, "faction");
                var attitudeText = ReadOptionalString(arguments, "attitude");
                Attitude? attitude = Enum.TryParse<Attitude>(attitudeText, ignoreCase: true, out var parsedAttitude)
                    ? parsedAttitude
                    : null;

                // Matched by name, not by id: the model is registering someone it just wrote
                // into the narration, and an id it supplied would be invented. Re-registering
                // an existing name therefore updates rather than duplicating — which is what
                // makes it safe to instruct the model to call this on every introduction.
                var lowered = name.ToLowerInvariant();
                var npc = await db.NPCs.FirstOrDefaultAsync(
                    n => n.GameId == gameId && n.Name.ToLower() == lowered, ct);

                var created = npc == null;
                if (npc == null)
                {
                    npc = new NPC { GameId = gameId, Name = name };
                    db.NPCs.Add(npc);
                }

                // Only overwrite what the model actually supplied — a later call that just
                // flips attitude must not blank out the description written on introduction.
                if (description is not null) npc.Description = description;
                if (faction is not null) npc.Faction = faction;
                if (attitude.HasValue) npc.Attitude = attitude.Value;

                // Registering someone is the GM putting them on stage, so this is also what
                // ranks them for the next turn's roster. A departed NPC written back into a
                // scene is simply back; the dead are not resurrected by a description edit —
                // that needs an explicit updateNPCStatus call.
                npc.LastSeenAt = DateTimeOffset.UtcNow;
                if (npc.Status == NPCStatus.Departed) npc.Status = NPCStatus.Active;

                await db.SaveChangesAsync(ct);

                return JsonSerializer.Serialize(new
                {
                    npc.Id,
                    npc.Name,
                    npc.Description,
                    Attitude = npc.Attitude.ToString(),
                    Status = npc.Status.ToString(),
                    npc.Faction,
                    result = created ? "created" : "updated"
                });
            }

            case "updateNPCStatus":
            {
                var name = ReadOptionalString(arguments, "name");
                if (name is null) return "Invalid or missing name";

                var statusText = ReadOptionalString(arguments, "status");
                if (!Enum.TryParse<NPCStatus>(statusText, ignoreCase: true, out var status))
                    return "Invalid status — expected Active, Dead or Departed";

                var lowered = name.ToLowerInvariant();
                var npc = await db.NPCs.FirstOrDefaultAsync(
                    n => n.GameId == gameId && n.Name.ToLower() == lowered, ct);
                // No silent create here: a status change naming someone who was never
                // registered means the model invented a name, and inventing a dead NPC to
                // match would put a corpse in the roster nobody ever met.
                if (npc == null) return $"No registered NPC named \"{name}\" in this game";

                npc.Status = status;
                npc.LastSeenAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(ct);

                var reason = ReadOptionalString(arguments, "reason");
                return JsonSerializer.Serialize(new
                {
                    npc.Id,
                    npc.Name,
                    Status = npc.Status.ToString(),
                    reason
                });
            }

            case "searchPlotContext":
                return await SearchCampaignAsync(gameId, ReadOptionalString(arguments, "query"), ct);

            case "updateGameState":
            {
                var state = arguments.GetProperty("state").GetString();
                var game = await db.Games.FindAsync([gameId], ct);
                if (game != null) { game.GameState = state; await db.SaveChangesAsync(ct); }
                return "Game state updated";
            }

            case "sendWhisper":
            {
                var content = ReadOptionalString(arguments, "content");
                if (content is null) return "Missing content";

                // By character name; targetPlayerId is still read for a model that learned it.
                Player? target = null;
                if (ReadOptionalString(arguments, "character") is { } whisperTo)
                {
                    var character = await characters.FindAsync(gameId, whisperTo, ct);
                    if (character is null)
                        return $"No player character called \"{whisperTo}\". The party: {await characters.PartyNamesAsync(gameId, ct)}.";
                    target = await db.Players.FirstOrDefaultAsync(p => p.Id == character.PlayerId, ct);
                }
                else if (TryGetGuid(arguments, "targetPlayerId", out var targetPlayerId))
                {
                    target = await db.Players.FirstOrDefaultAsync(p => p.Id == targetPlayerId && p.GameId == gameId, ct);
                }
                if (target == null) return $"Name the character to whisper to. The party: {await characters.PartyNamesAsync(gameId, ct)}.";

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
                // — otherwise every player would see it.
                var whisperDto = new MessageDto(whisperMsg.Id, sessionId, null, content, "Whisper", false, whisperMsg.CreatedAt, null);
                await hub.Clients.User(target.UserId.ToString()).SendAsync("NewMessage", whisperDto, ct);
                return $"Whisper sent to {target.CharacterName}";
            }

            case "startCombat":
            {
                var name = ReadOptionalString(arguments, "name") ?? "Combat";
                return await CombatToolAsync(async () =>
                {
                    var combat = await combats.StartAsync(gameId, sessionId, name,
                        ReadZones(arguments), ReadOptionalString(arguments, "partyZone"), ct);
                    return $"Combat \"{combat.Name}\" started. Zones: {string.Join(", ", combat.Zones.Select(z => z.Name))}. "
                        + $"In the fight: {string.Join(", ", combat.Participants.Select(p => p.DisplayName))}. Add the enemies now.";
                });
            }

            case "addCombatParticipant":
            {
                // "displayName" was this tool's old argument name; a model that learned it still works.
                var name = ReadOptionalString(arguments, "name") ?? ReadOptionalString(arguments, "displayName");
                if (name is null) return "Missing name";
                if (!TryGetInt(arguments, "hp", out var hp)) return "Missing or invalid hp";
                if (!TryGetInt(arguments, "ac", out var ac)) return "Missing or invalid ac";
                float? initiative = arguments.TryGetProperty("initiative", out var initEl) && initEl.TryGetSingle(out var i) ? i : null;
                return await ActiveCombatToolAsync(gameId, ct, async combat =>
                {
                    var p = await combats.AddParticipantAsync(combat, name, hp, ac, ReadOptionalString(arguments, "zone"), initiative, ct);
                    return $"{p.DisplayName} joins the combat (initiative {p.Initiative:0}).";
                });
            }

            case "moveParticipant":
            {
                var name = ReadOptionalString(arguments, "name");
                var zone = ReadOptionalString(arguments, "zone");
                if (name is null || zone is null) return "Missing name or zone";
                return await ActiveCombatToolAsync(gameId, ct, async combat =>
                {
                    var p = await combats.MoveAsync(combat, name, zone, ct);
                    return $"{p.DisplayName} is now in {zone}.";
                });
            }

            case "setZones":
                return await ActiveCombatToolAsync(gameId, ct, async combat =>
                {
                    await combats.SetZonesAsync(combat, ReadZones(arguments), ct);
                    return $"Zones are now: {string.Join(", ", combat.Zones.Select(z => z.Name))}.";
                });

            case "nextTurn":
                return await ActiveCombatToolAsync(gameId, ct, async combat =>
                {
                    if (combats.FightOver(combat) is { } over) return over;
                    var next = await combats.AdvanceTurnAsync(combat, ct);
                    return next is null ? "Nobody is in the combat." : DescribeTurn(combat, next);
                });

            case "endCombat":
                return await ActiveCombatToolAsync(gameId, ct, async combat =>
                {
                    await combats.EndAsync(combat, ReadOptionalString(arguments, "outcome"), ct);
                    return "Combat ended.";
                });

            case "generateLoot":
            {
                // The model was never told a combat id, so by default this is the most recent
                // fight in the game.
                Guid lootCombatId;
                if (TryGetGuid(arguments, "combatId", out var requestedCombatId))
                {
                    if (!await db.Combats.AnyAsync(c => c.Id == requestedCombatId && c.GameId == gameId, ct))
                        return "Combat not found in this game";
                    lootCombatId = requestedCombatId;
                }
                else
                {
                    var latest = await db.Combats.Where(c => c.GameId == gameId)
                        .OrderByDescending(c => c.CreatedAt).Select(c => (Guid?)c.Id).FirstOrDefaultAsync(ct);
                    if (latest is null) return "There has been no combat in this game";
                    lootCombatId = latest.Value;
                }

                var defeatedIds = new List<Guid>();
                if (arguments.TryGetProperty("defeatedNPCIds", out var idsEl) && idsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var idEl in idsEl.EnumerateArray())
                        if (idEl.ValueKind == JsonValueKind.String && Guid.TryParse(idEl.GetString(), out var npcId))
                            defeatedIds.Add(npcId);
                }
                // Nor was it told NPC ids: everyone registered who went down in that fight.
                if (defeatedIds.Count == 0)
                    defeatedIds = await db.CombatParticipants
                        .Where(p => p.CombatId == lootCombatId && p.NPCId != null && p.HP == 0)
                        .Select(p => p.NPCId!.Value)
                        .ToListAsync(ct);
                if (defeatedIds.Count == 0)
                    return "No defeated registered NPCs in that combat";

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

                // Loot is found, not handed out: who picks up what is a choice the players make
                // in the fiction, so the model is told to follow up with giveItem.
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
                return JsonSerializer.Serialize(new
                {
                    entries,
                    totalGold,
                    note = "Nothing is in anyone's inventory yet. When a character takes something, call giveItem for them (coins as \"Gold pieces\")."
                });
            }

            // Reached only for an attacker the GM controls; a player character's attack is
            // planned, rolled and finished as a player roll (see PlanPlayerRollAsync).
            case Attack:
                return await ActiveCombatToolAsync(gameId, ct, async combat =>
                {
                    var (attack, problem) = ReadAttack(combat, arguments);
                    if (attack is null) return TurnLog.Refused + problem;
                    var toHit = diceEngine.Roll($"1d20{attack.Bonus:+0;-0}");
                    return await LandAttackAsync(combat, attack, toHit, toHit.Total, toHit.NaturalD20, ct);
                });

            case "damage":
            case "heal":
            {
                var targetName = ReadOptionalString(arguments, "target") ?? ReadOptionalString(arguments, "character");
                if (targetName is null) return "Missing target";
                if (!TryGetInt(arguments, "amount", out var amount) || amount < 0) return "amount must be a non-negative integer";
                var signed = toolName == "damage" ? -amount : amount;
                var kind = ReadOptionalString(arguments, "type");

                // A combatant first: in a fight, the tracker owns hit points and writes a player
                // character's through to their sheet.
                var revive = ReadFlag(arguments, "revive") == true;
                const string deadNote = " is dead, and healing does not help the dead. If the story has truly raised them from death, repeat the call with \"revive\": true.";

                if (await combats.GetActiveAsync(gameId, ct) is { } combat && combats.FindByName(combat, targetName) is { } p)
                {
                    if (signed > 0 && combats.IsDead(p) && !revive) return p.DisplayName + deadNote;
                    var hp = await CombatToolAsync(async () => (await combats.ChangeHpAsync(combat, p.Id, signed, ct)).ToString());
                    if (!int.TryParse(hp, out var after)) return hp;
                    return after > 0 ? $"{p.DisplayName} is at {after}/{p.MaxHP} HP."
                        : p.ParticipantType == CombatParticipantType.Character
                            ? $"{p.DisplayName} drops to 0 HP and is dying — they make a death save at the start of each of their turns."
                            : $"{p.DisplayName} drops to 0 HP and is defeated.";
                }

                var character = await characters.FindAsync(gameId, targetName, ct);
                if (character is null)
                    return await UnknownTargetAsync(gameId, targetName, ct);
                if (CharacterStateService.IsDead(character))
                {
                    if (signed <= 0) return $"{character.Name} is already dead.";
                    if (!revive) return character.Name + deadNote;
                    await characters.SetConditionAsync(character, CharacterStateService.Dead, false, ct);
                }
                await characters.ChangeHpAsync(character, signed, ct);
                var what = toolName == "damage" ? $"takes {amount}{(kind is null ? "" : $" {kind}")} damage" : $"regains {amount} hit points";
                await characters.AnnounceAsync(gameId, sessionId, $"{character.Name} {what} ({character.CurrentHP}/{character.MaxHP} HP).", ct);
                return character.CurrentHP > 0
                    ? $"{character.Name} is at {character.CurrentHP}/{character.MaxHP} HP."
                    : $"{character.Name} drops to 0 HP and falls unconscious.";
            }

            case "setCondition":
            {
                var targetName = ReadOptionalString(arguments, "target") ?? ReadOptionalString(arguments, "character");
                var condition = ReadOptionalString(arguments, "condition");
                if (targetName is null || condition is null) return "Missing target or condition";
                var applied = !(arguments.TryGetProperty("applied", out var ap) && ap.ValueKind == JsonValueKind.False);

                if (await combats.GetActiveAsync(gameId, ct) is { } combat && combats.FindByName(combat, targetName) is { } p)
                {
                    await combats.SetConditionAsync(combat, p.Id, condition, applied, ct);
                    return $"{p.DisplayName} is {(applied ? "now" : "no longer")} {condition}.";
                }

                var character = await characters.FindAsync(gameId, targetName, ct);
                if (character is null)
                    return await UnknownTargetAsync(gameId, targetName, ct);
                await characters.SetConditionAsync(character, condition, applied, ct);
                return $"{character.Name} is {(applied ? "now" : "no longer")} {condition}.";
            }

            case "giveItem":
            case "removeItem":
            {
                var name = ReadOptionalString(arguments, "character");
                var item = ReadOptionalString(arguments, "item");
                if (name is null || item is null) return "Missing character or item";
                var quantity = TryGetInt(arguments, "quantity", out var q) && q > 0 ? q : 1;
                var character = await characters.FindAsync(gameId, name, ct);
                if (character is null)
                    return $"No player character called \"{name}\". The party: {await characters.PartyNamesAsync(gameId, ct)}.";

                if (toolName == "giveItem")
                {
                    var stack = await characters.GiveItemAsync(character, item, quantity, ct);
                    await characters.AnnounceAsync(gameId, sessionId, $"🎒 {character.Name} gains {Describe(item, quantity)}.", ct);
                    return $"{character.Name} now has {Describe(stack.Name, stack.Quantity)}.";
                }

                var left = await characters.RemoveItemAsync(character, item, quantity, ct);
                if (left is null)
                {
                    var carried = ClassRules.ReadInventory(character.Inventory).Select(i => i.Name);
                    return $"{character.Name} has no \"{item}\". They carry: {string.Join(", ", carried)}.";
                }
                await characters.AnnounceAsync(gameId, sessionId, $"🎒 {character.Name} loses {Describe(item, quantity)}.", ct);
                return left == 0 ? $"{character.Name} has no {item} left." : $"{character.Name} has {Describe(item, left.Value)} left.";
            }

            case "castSpell":
            {
                var name = ReadOptionalString(arguments, "character");
                var spell = ReadOptionalString(arguments, "spell") ?? "a spell";
                if (name is null) return "Missing character";
                if (!TryGetInt(arguments, "level", out var level) || level < 1 || level > 9)
                    return "level must be the spell's level, 1-9. Cantrips spend no slot — don't call castSpell for them.";
                var character = await characters.FindAsync(gameId, name, ct);
                if (character is null)
                    return $"No player character called \"{name}\". The party: {await characters.PartyNamesAsync(gameId, ct)}.";

                var spent = await characters.SpendSpellSlotAsync(character, level, ct);
                if (spent is null)
                    return $"{character.Name} has no spell slot of level {level} or higher left — {spell} cannot be cast. Narrate the attempt failing or let them choose something else.";
                var slots = ClassRules.ReadSlots(character.SpellSlots);
                var remaining = string.Join(", ", slots.OrderBy(kv => int.Parse(kv.Key)).Select(kv => $"L{kv.Key} {kv.Value.Remaining}/{kv.Value.Total}"));
                return $"{character.Name} casts {spell} using a level {spent} slot{(spent > level ? " (upcast)" : "")}. Slots left: {remaining}.";
            }

            case "rest":
            {
                var period = ReadOptionalString(arguments, "type")?.ToLowerInvariant() switch
                {
                    "long" or "longrest" => RestPeriod.LongRest,
                    "short" or "shortrest" => RestPeriod.ShortRest,
                    _ => (RestPeriod?)null
                };
                if (period is null) return "type must be \"short\" or \"long\"";
                if (await combats.GetActiveAsync(gameId, ct) is not null) return "Nobody can rest during combat. End the combat first.";

                var (party, unknown) = await characters.ResolveAsync(gameId, ReadStrings(arguments, "characters"), ct);
                var label = period == RestPeriod.LongRest ? "long rest" : "short rest";
                var lines = new List<string>();
                foreach (var c in party)
                    lines.Add($"{c.Name}: {await characters.RestAsync(c, period.Value, ct)}");
                if (lines.Count > 0)
                    await characters.AnnounceAsync(gameId, sessionId, $"🏕️ {string.Join(", ", party.Select(c => c.Name))} finish{(party.Count == 1 ? "es" : "")} a {label}.", ct);
                return string.Join("\n", lines.Concat(unknown.Select(u => $"No player character called \"{u}\".")));
            }

            case "awardExperience":
            {
                if (!TryGetInt(arguments, "amount", out var amount) || amount <= 0) return "amount must be a positive integer";
                var reason = ReadOptionalString(arguments, "reason");
                var (party, unknown) = await characters.ResolveAsync(gameId, ReadStrings(arguments, "characters"), ct);
                if (party.Count == 0) return "No player characters to award.";

                var lines = new List<string>();
                await characters.AnnounceAsync(gameId, sessionId,
                    $"✨ {string.Join(", ", party.Select(c => c.Name))} gain{(party.Count == 1 ? "s" : "")} {amount} XP{(reason is null ? "" : $" — {reason}")}.", ct);
                foreach (var c in party)
                {
                    var levelUps = await characters.AwardExperienceAsync(c, amount, ct);
                    foreach (var up in levelUps)
                    {
                        var extras = up.NewFeatures.Count > 0 ? $" New: {string.Join(", ", up.NewFeatures)}." : "";
                        var line = $"🎉 {c.Name} reaches level {up.Level}! +{up.HitPointsGained} max HP.{extras}";
                        await characters.AnnounceAsync(gameId, sessionId, line, ct);
                        lines.Add(line);
                    }
                    lines.Add($"{c.Name}: {c.Experience} XP, level {c.Level}"
                        + (ClassRules.XpForNextLevel(c.Level) is { } next ? $" (next at {next})" : " (max level)"));
                }
                return string.Join("\n", lines.Concat(unknown.Select(u => $"No player character called \"{u}\".")));
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
        var dc = ReadDc(arguments);
        var (content, success) = RollFormatting.Describe(formula, result, dc, reason);
        return (content, dc, success);
    }

    /// <summary>Names who could have been meant. In a fight the usual cause is an enemy the
    /// narration has but the tracker does not — observed live: the model narrated a hit on a
    /// goblin it had never added, and "the party: …" gave it nothing to correct that with.</summary>
    private async Task<string> UnknownTargetAsync(Guid gameId, string name, CancellationToken ct)
        => await combats.GetActiveAsync(gameId, ct) is { } combat
            ? $"\"{name}\" is not in the fight. In it: {string.Join(", ", combat.Participants.Select(p => p.DisplayName))}. If {name} is an enemy here, add them with addCombatParticipant first, then repeat this call."
            : $"No combatant or player character called \"{name}\". The party: {await characters.PartyNamesAsync(gameId, ct)}.";

    private sealed record AttackCall(CombatParticipant Attacker, CombatParticipant Target, int Bonus, string Damage,
        string? Kind, string? MoveTo, bool EndTurn, bool Reaction);

    /// <summary>An attack as the model asked for it, checked against the fight as it stands —
    /// or what to tell the model instead. Changes nothing, so it can be asked at every stage
    /// of a player's roll.</summary>
    private (AttackCall? Attack, string? Problem) ReadAttack(CombatEntity combat, JsonElement arguments)
    {
        var attackerName = ReadOptionalString(arguments, "attacker");
        var targetName = ReadOptionalString(arguments, "target");
        var damage = ReadOptionalString(arguments, "damage");
        if (attackerName is null || targetName is null || damage is null) return (null, "Missing attacker, target or damage");
        TryGetInt(arguments, "bonus", out var bonus);
        var reaction = ReadFlag(arguments, "reaction") == true;

        var everyone = string.Join(", ", combat.Participants.Select(x => x.DisplayName));
        if (combats.FindByName(combat, attackerName) is not { } attacker)
            return (null, $"No combatant called \"{attackerName}\". Combatants: {everyone}.");
        if (combats.FindByName(combat, targetName) is not { } target)
            return (null, $"No combatant called \"{targetName}\". Combatants: {everyone}. If {targetName} is an enemy here, add them with addCombatParticipant first, then repeat this call.");

        // Enforced here because a prompt alone did not hold: the model rolled for enemies
        // while it was still the player character's turn.
        if (!reaction && combats.Acting(combat) is { } acting && acting != attacker)
            return (null, $"It is {acting.DisplayName}'s turn, not {attacker.DisplayName}'s. "
                + (acting.ParticipantType == CombatParticipantType.Character
                    ? $"If {acting.DisplayName} has done what their player said, call nextTurn; otherwise finish with your narration and wait for their player."
                    : $"Play {acting.DisplayName}'s turn first, or call nextTurn to pass it.")
                + " (An attack outside the attacker's own turn needs \"reaction\": true.)");
        if (attacker.HP == 0)
            return (null, combats.IsDying(attacker)
                ? $"{attacker.DisplayName} is dying and cannot attack: call deathSave for them."
                : $"{attacker.DisplayName} is down and cannot attack. Call nextTurn.");

        if (target.HP == 0)
        {
            if (target.ParticipantType != CombatParticipantType.Character)
                return (null, $"{target.DisplayName} is already defeated. Pick a target still standing, or call nextTurn.");
            if (!combats.IsDying(target))
                return (null, $"{target.DisplayName} is no longer a threat or a target. Pick someone still standing, or call nextTurn.");
            // Several enemies' turns can be sent in one reply, chosen before the first blow
            // landed. Without this, the second and third piled onto whoever the first dropped.
            if (ReadFlag(arguments, "evenIfDown") != true)
                return (null, $"{target.DisplayName} is already down and dying. Striking a dying character is a deliberate choice that can kill them: repeat the call with \"evenIfDown\": true if {attacker.DisplayName} really would, or pick another target.");
        }

        var moveTo = ReadOptionalString(arguments, "moveTo");
        if (moveTo is not null && !combat.Zones.Any(z => z.Name.Equals(moveTo, StringComparison.OrdinalIgnoreCase)))
            return (null, $"No zone called \"{moveTo}\". Zones: {string.Join(", ", combat.Zones.Select(z => z.Name))}.");

        // Checked before anything is rolled or posted, so a formula the dice engine rejects
        // fails the call instead of leaving half an attack in the log.
        try { diceEngine.Roll(damage); }
        catch (Exception ex) { return (null, $"\"{damage}\" is not a damage formula the dice engine can roll ({ex.Message}). Use e.g. 1d8+3."); }

        return (new AttackCall(attacker, target, bonus, damage, ReadOptionalString(arguments, "type"), moveTo,
            !reaction && ReadFlag(arguments, "endTurn") != false, reaction), null);
    }

    /// <summary>
    /// What a to-hit roll does: the miss, or the damage and what it leaves of the target, and
    /// then the end of the attacker's turn. <paramref name="toHit"/> is null for a player
    /// character, whose roll the table has already seen as their own message.
    /// </summary>
    private async Task<string> LandAttackAsync(CombatEntity combat, AttackCall attack, DiceResult? toHit, int total, int? naturalD20, CancellationToken ct)
    {
        var (attacker, target) = (attack.Attacker, attack.Target);
        if (attack.MoveTo is { } zone)
            await combats.MoveAsync(combat, attacker.DisplayName, zone, ct);

        var critical = naturalD20 == 20;
        var hit = naturalD20 != 1 && (critical || total >= target.AC);
        var metadata = new { combatId = combat.Id };
        var line = toHit is null ? "" : $"⚔️ {attacker.DisplayName} attacks {target.DisplayName}: {toHit.Breakdown} vs AC {target.AC} — ";

        if (!hit)
        {
            if (toHit is null)
            {
                line = $"{attacker.DisplayName} misses {target.DisplayName}.";
            }
            else
            {
                line += "miss.";
                await BroadcastRollAsync(combat.GameId, combat.SessionId, line, "Combat", metadata, ct);
            }
        }
        else
        {
            var damageRoll = diceEngine.Roll(critical ? DoubleDice(attack.Damage) : attack.Damage);
            var amount = Math.Max(0, damageRoll.Total);
            var kind = attack.Kind is null ? "" : $" {attack.Kind}";
            var wasDown = target.HP == 0;
            var left = Math.Max(0, target.HP - amount);
            line += toHit is null
                ? $"⚔️ {attacker.DisplayName} {(critical ? "lands a critical hit on" : "hits")} {target.DisplayName}: {damageRoll.Breakdown}{kind} damage."
                : $"{(critical ? "critical hit" : "hit")} for {amount}{kind} damage.";
            if (!wasDown)
                line += left > 0 ? $" {target.DisplayName} is at {left}/{target.MaxHP} HP."
                    : target.ParticipantType == CombatParticipantType.Character ? $" {target.DisplayName} drops to 0 HP and is dying."
                    : $" {target.DisplayName} is defeated.";

            // Posted before the hit points change, so that what the tracker adds about a
            // character hit while down reads after the blow that did it.
            await BroadcastRollAsync(combat.GameId, combat.SessionId, line, "Combat", metadata, ct);
            await combats.ChangeHpAsync(combat, target.Id, -amount, ct, critical);
            if (wasDown && target.ParticipantType == CombatParticipantType.Character)
                line += combats.IsDying(target) ? $" {target.DisplayName} is still down and has failed a death save." : $" {target.DisplayName} is dead.";
        }

        var result = $"{line} Hit points are already updated — do not call damage for this attack.";

        // Passing the turn on in a fight that is over only announces a turn nobody will take.
        if (combats.FightOver(combat) is { } over)
            return $"{result} {over}";

        if (!attack.EndTurn || combats.Acting(combat) != attacker)
            return attack.Reaction
                ? $"{result} That was a reaction; the turn has not changed."
                : $"{result} It is still {attacker.DisplayName}'s turn: finish it, then call nextTurn.";

        var next = await combats.AdvanceTurnAsync(combat, ct);
        return $"{result} {attacker.DisplayName}'s turn is over." + (next is null ? "" : $" {DescribeTurn(combat, next)}");
    }

    private static bool? ReadFlag(JsonElement arguments, string property)
        => arguments.ValueKind == JsonValueKind.Object && arguments.TryGetProperty(property, out var el)
            ? el.ValueKind switch { JsonValueKind.True => true, JsonValueKind.False => false, _ => null }
            : null;

    /// <summary>Whose turn it now is, and whose move that makes it.</summary>
    private string DescribeTurn(CombatEntity combat, CombatParticipant next)
        => $"Round {combat.CurrentRound}: it is now {next.DisplayName}'s turn"
           + (combats.IsDying(next)
               ? $" — they are dying: call deathSave for {next.DisplayName} now. That is their whole turn and it passes the turn on."
               : next.ParticipantType == CombatParticipantType.Character
               ? $" — the next move is their player's: finish with your narration and no more tool calls. Say at most what {next.DisplayName} now faces; never describe {next.DisplayName} doing anything their player has not said."
               : " — you play them. In one reply: a sentence saying what they attempt, together with the attack call that resolves it; the attack ends their turn by itself.");

    /// <summary>A critical hit rolls the damage dice twice; the modifier is added once.</summary>
    private static string DoubleDice(string formula)
        => System.Text.RegularExpressions.Regex.Replace(formula, @"(\d*)d(\d+)",
            m => $"{(m.Groups[1].Value.Length == 0 ? 1 : int.Parse(m.Groups[1].Value)) * 2}d{m.Groups[2].Value}");

    private static string Describe(string item, int quantity) => quantity == 1 ? item : $"{item} ×{quantity}";

    private static List<string>? ReadStrings(JsonElement arguments, string property)
        => arguments.ValueKind == JsonValueKind.Object
           && arguments.TryGetProperty(property, out var el) && el.ValueKind == JsonValueKind.Array
            ? el.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()!).ToList()
            : null;

    private static List<string> FeatureNames(JsonElement features)
        => features.ValueKind != JsonValueKind.Array
            ? []
            : features.EnumerateArray()
                .Where(f => f.ValueKind == JsonValueKind.Object && f.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String)
                .Select(f =>
                {
                    var name = f.GetProperty("name").GetString()!;
                    return f.TryGetProperty("usesRemaining", out var u) && u.ValueKind == JsonValueKind.Number
                        ? $"{name} ({u.GetInt32()} uses left)" : name;
                })
                .ToList();

    /// <summary>
    /// A plain substring search, not a vector one: the model asks about a name or a place,
    /// which is exactly what a substring finds, and it costs no embedding call per turn. Only
    /// what the narrator may see is searched — see MessageVisibility.
    /// </summary>
    private async Task<string> SearchCampaignAsync(Guid gameId, string? query, CancellationToken ct)
    {
        if (query is null || query.Length < 3) return "Give a search term of at least 3 characters.";
        var pattern = $"%{query.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";

        var messages = await db.Messages
            .Where(m => m.Session.GameId == gameId && EF.Functions.ILike(m.Content, pattern))
            .VisibleToNarration()
            .OrderByDescending(m => m.CreatedAt)
            .Take(8)
            .Select(m => new { m.CreatedAt, Speaker = m.Player != null ? m.Player.CharacterName : "GM", m.Content })
            .ToListAsync(ct);
        var threads = await db.PlotThreads
            .Where(t => t.GameId == gameId && (EF.Functions.ILike(t.Title, pattern) || (t.Description != null && EF.Functions.ILike(t.Description, pattern))))
            .Take(5)
            .Select(t => new { t.Title, t.Description, Status = t.Status.ToString() })
            .ToListAsync(ct);
        var npcs = await db.NPCs
            .Where(n => n.GameId == gameId && (EF.Functions.ILike(n.Name, pattern) || (n.Description != null && EF.Functions.ILike(n.Description, pattern))))
            .Take(5)
            .Select(n => new { n.Name, n.Description, Status = n.Status.ToString() })
            .ToListAsync(ct);

        if (messages.Count == 0 && threads.Count == 0 && npcs.Count == 0)
            return $"Nothing in the campaign mentions \"{query}\".";

        return JsonSerializer.Serialize(new
        {
            messages = messages.Select(m => new
            {
                when = m.CreatedAt.ToString("yyyy-MM-dd"),
                m.Speaker,
                content = m.Content.Length > 400 ? m.Content[..400] + "…" : m.Content
            }),
            plotThreads = threads,
            npcs
        });
    }

    internal static int? ReadDc(JsonElement arguments)
        => arguments.ValueKind == JsonValueKind.Object
           && arguments.TryGetProperty("dc", out var dcEl)
           && dcEl.ValueKind == JsonValueKind.Number
            ? dcEl.GetInt32()
            : null;

    // GM-tool rolls must land their own chat message immediately,
    // not rely on the follow-up narrator LLM call to happen to mention the numbers — that call
    // is prose-only and routinely narrates around the roll without ever stating it.
    private async Task BroadcastRollAsync(Guid gameId, Guid sessionId, string content, string type, object? metadata, CancellationToken ct, Guid? playerId = null)
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
