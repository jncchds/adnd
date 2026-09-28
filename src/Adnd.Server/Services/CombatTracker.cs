using System.Text;
using System.Text.Json;
using Adnd.Server.Data;
using Adnd.Server.Events;
using Adnd.Server.Hubs;
using Adnd.Server.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using CombatEntity = Adnd.Server.Models.Combat;

namespace Adnd.Server.Services;

/// <summary>A combat rule was broken — surfaced to a hub caller as a HubException and to the
/// model as the tool result, so it can correct itself.</summary>
public class CombatRuleException(string message) : Exception(message);

public record ZoneSpec(string Name, IReadOnlyList<string> Tags);

/// <summary>
/// The one place combat state changes. Only the AI GM's tools call it — the LLM runs the
/// fight, and the game's creator has no manual controls — and every change is followed by a
/// <c>CombatUpdated</c> push carrying the whole combat. The AI's <c>startCombat</c> used to
/// write rows and broadcast nothing, so a fight the narrator started never appeared for anyone.
///
/// Position is a zone, not a square. The table plays in chat; "I run to the tower door" is
/// something a player says, and the narrator turns it into <see cref="MoveAsync"/>. Zones are
/// kept in order and neighbours in that order are adjacent.
/// </summary>
public interface ICombatTracker
{
    Task<CombatEntity?> GetActiveAsync(Guid gameId, CancellationToken ct = default);

    /// <summary>Starts a fight with every player character already in it, initiative rolled.</summary>
    Task<CombatEntity> StartAsync(Guid gameId, Guid sessionId, string name, IReadOnlyList<ZoneSpec> zones, string? partyZone, CancellationToken ct = default);
    Task<CombatParticipant> AddParticipantAsync(CombatEntity combat, string name, int hp, int ac, string? zone, float? initiative, CancellationToken ct = default);
    Task SetZonesAsync(CombatEntity combat, IReadOnlyList<ZoneSpec> zones, CancellationToken ct = default);
    Task<CombatParticipant> MoveAsync(CombatEntity combat, string participantName, string zoneName, CancellationToken ct = default);
    Task<CombatParticipant?> AdvanceTurnAsync(CombatEntity combat, CancellationToken ct = default);

    /// <summary>A combatant by display name, case-insensitively — how the model addresses them.</summary>
    CombatParticipant? FindByName(CombatEntity combat, string name);

    /// <summary>True for a player character at 0 HP who is neither dead nor stable.</summary>
    bool IsDying(CombatParticipant participant);

    /// <summary>Negative amount is damage. A player character's sheet follows its participant.
    /// Returns the combatant's hit points afterwards.</summary>
    Task<int> ChangeHpAsync(CombatEntity combat, Guid participantId, int amount, CancellationToken ct = default);
    Task SetConditionAsync(CombatEntity combat, Guid participantId, string condition, bool applied, CancellationToken ct = default);
    /// <summary>Returns the outcome as the log states it.</summary>
    Task<string> RecordDeathSaveAsync(CombatEntity combat, Guid participantId, int roll, CancellationToken ct = default);
    Task EndAsync(CombatEntity combat, string? outcome, CancellationToken ct = default);

    CombatDto ToDto(CombatEntity combat);

    /// <summary>The combat block for the narrator's system prompt, or null when nobody is fighting.</summary>
    Task<string?> DescribeForNarratorAsync(Guid gameId, Guid? speakingPlayerId, CancellationToken ct = default);
}

public class CombatTracker(
    AppDbContext db,
    IDiceEngine dice,
    IEventBus eventBus,
    IHubContext<GameHub> hub) : ICombatTracker
{
    private const string DefaultZone = "Battlefield";

    public Task<CombatEntity?> GetActiveAsync(Guid gameId, CancellationToken ct = default)
        => db.Combats.Include(c => c.Participants)
            .Where(c => c.GameId == gameId && c.Status != CombatStatus.Finished)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync(ct);

    public async Task<CombatEntity> StartAsync(Guid gameId, Guid sessionId, string name, IReadOnlyList<ZoneSpec> zones, string? partyZone, CancellationToken ct = default)
    {
        // One fight at a time. A second startCombat from the model is almost always the same
        // fight described twice, and two active combats would give the table two turn orders.
        if (await GetActiveAsync(gameId, ct) is { } existing)
            throw new CombatRuleException($"Combat \"{existing.Name}\" is already in progress. Add combatants to it, or end it first.");

        var combat = new CombatEntity
        {
            GameId = gameId,
            SessionId = sessionId,
            Name = string.IsNullOrWhiteSpace(name) ? "Combat" : name.Trim(),
            Status = CombatStatus.Active,
            CurrentRound = 1,
            Zones = BuildZones(zones),
        };
        db.Combats.Add(combat);

        var startZone = FindZone(combat, partyZone) ?? combat.Zones[0];

        // Every player character is in the fight from the start — the model was never given
        // their ids, and leaving the party out of its own turn order made the tracker useless.
        var characters = await db.Characters
            .Where(c => c.Player.GameId == gameId)
            .ToListAsync(ct);

        foreach (var ch in characters)
        {
            var dex = DexModifier(ch.Attributes);
            combat.Participants.Add(new CombatParticipant
            {
                CombatId = combat.Id,
                ParticipantType = CombatParticipantType.Character,
                CharacterId = ch.Id,
                PlayerId = ch.PlayerId,
                DisplayName = ch.Name,
                HP = ch.CurrentHP,
                MaxHP = ch.MaxHP,
                // Characters carry no armour class yet; unarmoured AC is the honest default
                // until the sheet has a real one.
                AC = 10 + dex,
                // Initiative is a roll the rules give nobody a choice about, so it is simply made.
                Initiative = dice.Roll("1d20").Total + dex,
                ZoneId = startZone.Id,
                // A poisoned character walks into the fight still poisoned.
                Conditions = ch.Conditions.ValueKind == JsonValueKind.Array ? ch.Conditions.Clone() : JsonSerializer.SerializeToElement(Array.Empty<string>()),
            });
        }

        await db.SaveChangesAsync(ct);

        var order = Ordered(combat).ToList();
        var lines = new StringBuilder($"⚔️ Combat begins: {combat.Name}.");
        if (order.Count > 0)
            lines.Append(" Initiative — ").Append(string.Join(", ", order.Select(p => $"{p.DisplayName} {p.Initiative:0}")));
        await PostAsync(combat, lines.ToString(), ct);

        await PushAsync(combat, ct);
        await eventBus.PublishAsync(new CombatStarted(gameId, combat.Id, combat.Name), ct);
        return combat;
    }

    public async Task<CombatParticipant> AddParticipantAsync(CombatEntity combat, string name, int hp, int ac, string? zone, float? initiative, CancellationToken ct = default)
    {
        name = name.Trim();
        if (name.Length == 0) throw new CombatRuleException("A combatant needs a name.");
        if (hp <= 0) throw new CombatRuleException("A combatant needs positive hit points.");
        if (Ordered(combat).Any(p => p.DisplayName.Equals(name, StringComparison.OrdinalIgnoreCase)))
            throw new CombatRuleException($"\"{name}\" is already in this combat. Give each combatant a distinct name, e.g. \"{name} 2\".");

        var targetZone = zone is null ? combat.Zones.FirstOrDefault() : FindZone(combat, zone)
            ?? throw new CombatRuleException($"No zone called \"{zone}\". Zones: {ZoneList(combat)}.");

        // Linked to the NPC roster by name, as registerNPC matches — that link is what lets
        // generateLoot find what a defeated enemy was carrying.
        var lowered = name.ToLowerInvariant();
        var npc = await db.NPCs.FirstOrDefaultAsync(n => n.GameId == combat.GameId && n.Name.ToLower() == lowered, ct);

        var anchor = Current(combat);
        var participant = new CombatParticipant
        {
            CombatId = combat.Id,
            ParticipantType = CombatParticipantType.NPC,
            NPCId = npc?.Id,
            DisplayName = name,
            HP = hp,
            MaxHP = hp,
            AC = ac,
            Initiative = initiative ?? dice.Roll("1d20").Total,
            ZoneId = targetZone?.Id,
        };
        db.CombatParticipants.Add(participant);
        combat.Participants.Add(participant);
        Reanchor(combat, anchor);
        await db.SaveChangesAsync(ct);

        await PushAsync(combat, ct);
        await eventBus.PublishAsync(new ParticipantAdded(combat.GameId, combat.Id, participant.Id, name), ct);
        return participant;
    }

    public async Task SetZonesAsync(CombatEntity combat, IReadOnlyList<ZoneSpec> zones, CancellationToken ct = default)
    {
        combat.Zones = BuildZones(zones);
        // Someone standing in a zone that no longer exists is put in the first one rather
        // than left nowhere — the narrator can move them.
        foreach (var p in combat.Participants.Where(p => FindZoneById(combat, p.ZoneId) is null))
            p.ZoneId = combat.Zones[0].Id;
        await db.SaveChangesAsync(ct);
        await PushAsync(combat, ct);
    }

    public async Task<CombatParticipant> MoveAsync(CombatEntity combat, string participantName, string zoneName, CancellationToken ct = default)
    {
        var p = FindParticipant(combat, participantName)
            ?? throw new CombatRuleException($"No combatant called \"{participantName}\". Combatants: {string.Join(", ", Ordered(combat).Select(x => x.DisplayName))}.");
        var zone = FindZone(combat, zoneName)
            ?? throw new CombatRuleException($"No zone called \"{zoneName}\". Zones: {ZoneList(combat)}.");

        p.ZoneId = zone.Id;
        await db.SaveChangesAsync(ct);
        await PushAsync(combat, ct);
        return p;
    }

    public async Task<CombatParticipant?> AdvanceTurnAsync(CombatEntity combat, CancellationToken ct = default)
    {
        var order = Ordered(combat).ToList();
        if (order.Count == 0) return null;

        var index = combat.CurrentTurnIndex;
        // Defeated enemies are skipped rather than removed, so the log still shows them, and so
        // are player characters who are dead or stable — neither has anything to do. A dying
        // character still gets a turn: that is when they make their death save.
        for (var i = 0; i < order.Count; i++)
        {
            index++;
            if (index >= order.Count) { index = 0; combat.CurrentRound++; }
            if (!IsOutOfTheFight(order[index])) break;
        }

        combat.CurrentTurnIndex = index;
        var current = order[index];
        // The action economy was never reset, so whatever the panel showed stayed spent forever.
        current.ActionsRemaining = 1;
        current.BonusActionsRemaining = 1;
        current.ReactionsRemaining = 1;
        current.MovementsRemaining = 30;
        combat.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        // The chat is the table: the turn is announced there, where players are looking.
        await PostAsync(combat, $"Round {combat.CurrentRound} — {current.DisplayName}'s turn.", ct);
        await PushAsync(combat, ct);
        await eventBus.PublishAsync(new TurnAdvanced(combat.GameId, combat.Id, combat.CurrentRound, index), ct);
        return current;
    }

    public CombatParticipant? FindByName(CombatEntity combat, string name) => FindParticipant(combat, name);

    public bool IsDying(CombatParticipant p)
        => p.ParticipantType == CombatParticipantType.Character && p.HP == 0
           && ReadDeathSaves(p.DeathSaveState) is not { IsDead: true } and not { IsStable: true };

    public async Task<int> ChangeHpAsync(CombatEntity combat, Guid participantId, int amount, CancellationToken ct = default)
    {
        var p = Require(combat, participantId);
        var wasDown = p.HP == 0;
        p.HP = Math.Clamp(p.HP + amount, 0, p.MaxHP);

        // Damage while down, or any healing back above zero, restarts the death-save clock.
        if (p.HP > 0 || (wasDown && amount < 0)) p.DeathSaveState = default;

        // The sheet and the tracker used to be two unrelated hit-point counts.
        if (p.CharacterId is { } characterId
            && await db.Characters.FirstOrDefaultAsync(c => c.Id == characterId, ct) is { } character)
        {
            character.CurrentHP = p.HP;
        }

        await db.SaveChangesAsync(ct);
        await PushAsync(combat, ct);
        if (p.CharacterId is { } changed)
            await hub.Clients.Group(combat.GameId.ToString()).SendAsync("CharacterUpdated", new { characterId = changed }, ct);

        if (amount < 0)
            await eventBus.PublishAsync(new CombatDamageDealt(combat.GameId, combat.Id, participantId, -amount, "Physical"), ct);
        else
            await eventBus.PublishAsync(new CombatHealed(combat.GameId, combat.Id, participantId, amount), ct);
        return p.HP;
    }

    public async Task SetConditionAsync(CombatEntity combat, Guid participantId, string condition, bool applied, CancellationToken ct = default)
    {
        var p = Require(combat, participantId);
        var conditions = ReadConditions(p.Conditions).Where(c => !c.Equals(condition, StringComparison.OrdinalIgnoreCase)).ToList();
        if (applied) conditions.Add(condition);
        p.Conditions = JsonSerializer.SerializeToElement(conditions);

        // A player character's conditions outlast the fight, so the sheet carries them too.
        if (p.CharacterId is { } characterId
            && await db.Characters.FirstOrDefaultAsync(c => c.Id == characterId, ct) is { } character)
            character.Conditions = CharacterStateService.WithCondition(character.Conditions, condition, applied);

        await db.SaveChangesAsync(ct);
        await PushAsync(combat, ct);
        if (p.CharacterId is { } changed)
            await hub.Clients.Group(combat.GameId.ToString()).SendAsync("CharacterUpdated", new { characterId = changed }, ct);
    }

    public async Task<string> RecordDeathSaveAsync(CombatEntity combat, Guid participantId, int roll, CancellationToken ct = default)
    {
        var p = Require(combat, participantId);
        if (p.HP > 0) throw new CombatRuleException($"{p.DisplayName} is not dying.");

        var state = ReadDeathSaves(p.DeathSaveState) ?? new DeathSaveStateDto(0, 0, false, false);
        if (state.IsDead || state.IsStable) throw new CombatRuleException($"{p.DisplayName} no longer makes death saves.");

        if (roll >= 20)
        {
            // A natural 20 brings the character back with 1 hit point.
            await ChangeHpAsync(combat, participantId, 1, ct);
            var revived = $"💀 {p.DisplayName} rolls a 20 on a death save and regains consciousness with 1 hit point!";
            await PostAsync(combat, revived, ct);
            return revived;
        }

        var successes = state.Successes + (roll >= 10 ? 1 : 0);
        var failures = state.Failures + (roll <= 1 ? 2 : roll < 10 ? 1 : 0);
        var dead = failures >= 3;
        var stable = !dead && successes >= 3;
        p.DeathSaveState = JsonSerializer.SerializeToElement(new
        {
            successes = Math.Min(successes, 3),
            failures = Math.Min(failures, 3),
            isDead = dead,
            isStable = stable,
        });
        var outcome = dead ? " — they die." : stable ? " — they are stable." : $" ({successes}✓ {failures}✗)";

        await db.SaveChangesAsync(ct);
        var line = $"💀 {p.DisplayName} death save: {roll}{outcome}";
        await PostAsync(combat, line, ct);
        await PushAsync(combat, ct);
        return line;
    }

    public async Task EndAsync(CombatEntity combat, string? outcome, CancellationToken ct = default)
    {
        combat.Status = CombatStatus.Finished;
        combat.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        var text = string.IsNullOrWhiteSpace(outcome) ? $"Combat ends: {combat.Name}." : $"Combat ends: {outcome.Trim()}";
        await PostAsync(combat, text, ct);
        await hub.Clients.Group(combat.GameId.ToString()).SendAsync("CombatEnded", new { combatId = combat.Id }, ct);

        var defeated = combat.Participants
            .Where(p => p.NPCId != null && p.HP == 0)
            .Select(p => p.NPCId!.Value)
            .ToList();
        await eventBus.PublishAsync(new CombatEnded(combat.GameId, combat.Id, defeated), ct);
    }

    public CombatDto ToDto(CombatEntity combat) => new(
        combat.Id, combat.GameId, combat.SessionId, combat.Name, combat.Status.ToString(),
        combat.CurrentRound, combat.CurrentTurnIndex,
        combat.Zones.Select(z => new CombatZoneDto(z.Id, z.Name, z.Tags)).ToList(),
        Ordered(combat).Select(p => new ParticipantDto(
            p.Id, p.DisplayName, p.Initiative, p.HP, p.MaxHP, p.AC, p.ParticipantType.ToString(),
            ReadConditions(p.Conditions),
            p.ActionsRemaining, p.BonusActionsRemaining, p.ReactionsRemaining, p.MovementsRemaining,
            ReadDeathSaves(p.DeathSaveState),
            p.ZoneId, p.CharacterId, p.PlayerId)).ToList());

    public async Task<string?> DescribeForNarratorAsync(Guid gameId, Guid? speakingPlayerId, CancellationToken ct = default)
    {
        if (await GetActiveAsync(gameId, ct) is not { } combat) return null;

        var order = Ordered(combat).ToList();
        var current = Current(combat);
        var sb = new StringBuilder();
        sb.AppendLine($"=== COMBAT IN PROGRESS: {combat.Name} — round {combat.CurrentRound} ===");
        sb.AppendLine($"Zones, in order (neighbours are adjacent): {string.Join(" | ", combat.Zones.Select(z => z.Tags.Count > 0 ? $"{z.Name} [{string.Join(", ", z.Tags)}]" : z.Name))}");
        sb.AppendLine("Turn order (▶ = acting now):");
        foreach (var p in order)
        {
            var marker = p == current ? "▶ " : "  ";
            var kind = p.ParticipantType == CombatParticipantType.Character ? " (PC)" : "";
            var zone = FindZoneById(combat, p.ZoneId)?.Name ?? "?";
            var conditions = ReadConditions(p.Conditions);
            var saves = ReadDeathSaves(p.DeathSaveState);
            var state = IsDefeatedEnemy(p) ? " — DEFEATED"
                : saves is { IsDead: true } ? " — DEAD"
                : saves is { IsStable: true } ? " — unconscious but stable"
                : p.HP == 0 ? " — DOWN, making death saves"
                : conditions.Count > 0 ? $" — {string.Join(", ", conditions)}" : "";
            sb.AppendLine($"{marker}{p.DisplayName}{kind} — {zone} — HP {p.HP}/{p.MaxHP}, AC {p.AC}{state}");
        }

        if (speakingPlayerId is { } speaker)
        {
            var own = order.FirstOrDefault(p => p.PlayerId == speaker);
            if (own is not null)
                sb.AppendLine(own == current
                    ? $"The player speaking now plays {own.DisplayName}, and it IS their turn: resolve what they do."
                    : $"The player speaking now plays {own.DisplayName}, and it is NOT their turn ({current?.DisplayName} is acting). Treat what they say as table talk, a reaction or a free action — do not let them take a full turn out of order.");
        }

        sb.AppendLine("Combat rules: combatants in the same zone can reach each other in melee. Moving to an adjacent zone takes the combatant's move; further takes their action too (a dash). Ranged attacks reach any zone; more than one zone away is long range (disadvantage).");
        sb.AppendLine("Hit points and conditions change ONLY through tools: call damage when an attack or effect hits (after the roll has decided it does), heal for healing, and setCondition to apply or remove a condition. Narrating a wound does not change anyone's hit points. An enemy at 0 HP is defeated; a player character at 0 HP is dying.");
        sb.AppendLine("Call moveParticipant whenever someone changes zone. When the acting combatant's turn is resolved, call nextTurn. On an enemy's turn, decide what it does, roll for it with rollDice, apply the result with damage, narrate it, then call nextTurn. On a player character's turn, ask for their attack or save with requestPlayerRoll. When a dying player character's turn comes, call deathSave for them. Add newly arriving combatants with addCombatParticipant. Call endCombat when one side is defeated, flees or surrenders.");
        return sb.ToString();
    }

    // ── helpers ──

    /// <summary>The turn order. CurrentTurnIndex indexes into this, so everything uses it.</summary>
    private static IEnumerable<CombatParticipant> Ordered(CombatEntity combat)
        => combat.Participants
            .OrderByDescending(p => p.Initiative)
            .ThenBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.Id);

    private static CombatParticipant? Current(CombatEntity combat)
        => Ordered(combat).ElementAtOrDefault(combat.CurrentTurnIndex);

    /// <summary>Keeps the turn with the same combatant after the order changed underneath it.</summary>
    private static void Reanchor(CombatEntity combat, CombatParticipant? anchor)
    {
        var order = Ordered(combat).ToList();
        var index = anchor is null ? -1 : order.IndexOf(anchor);
        combat.CurrentTurnIndex = index >= 0 ? index : Math.Clamp(combat.CurrentTurnIndex, 0, Math.Max(order.Count - 1, 0));
    }

    private static CombatParticipant Require(CombatEntity combat, Guid participantId)
        => combat.Participants.FirstOrDefault(p => p.Id == participantId)
           ?? throw new CombatRuleException("That combatant is not in this combat.");

    private static CombatParticipant? FindParticipant(CombatEntity combat, string name)
        => combat.Participants.FirstOrDefault(p => p.DisplayName.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));

    private static bool IsDefeatedEnemy(CombatParticipant p)
        => p.ParticipantType != CombatParticipantType.Character && p.HP == 0;

    private static bool IsOutOfTheFight(CombatParticipant p)
        => IsDefeatedEnemy(p) || ReadDeathSaves(p.DeathSaveState) is { IsDead: true } or { IsStable: true };

    private static List<CombatZone> BuildZones(IReadOnlyList<ZoneSpec> zones)
    {
        var built = zones
            .Where(z => !string.IsNullOrWhiteSpace(z.Name))
            .Select(z => new CombatZone
            {
                Id = z.Name.Trim().ToLowerInvariant(),
                Name = z.Name.Trim(),
                Tags = z.Tags.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()).ToList(),
            })
            .DistinctBy(z => z.Id)
            .ToList();
        return built.Count > 0 ? built : [new CombatZone { Id = DefaultZone.ToLowerInvariant(), Name = DefaultZone }];
    }

    private static CombatZone? FindZone(CombatEntity combat, string? name)
        => string.IsNullOrWhiteSpace(name) ? null : FindZoneById(combat, name.Trim().ToLowerInvariant());

    private static CombatZone? FindZoneById(CombatEntity combat, string? id)
        => id is null ? null : combat.Zones.FirstOrDefault(z => z.Id == id);

    private static string ZoneList(CombatEntity combat) => string.Join(", ", combat.Zones.Select(z => z.Name));

    private static int DexModifier(JsonElement attributes)
        => attributes.ValueKind == JsonValueKind.Object
           && attributes.TryGetProperty("DEX", out var dex) && dex.TryGetInt32(out var score)
            ? AbilityScoreHelper.GetModifier(score)
            : 0;

    private async Task PushAsync(CombatEntity combat, CancellationToken ct)
        => await hub.Clients.Group(combat.GameId.ToString()).SendAsync("CombatUpdated", ToDto(combat), ct);

    /// <summary>Everything is a message: the fight's milestones go into the log where the table reads.</summary>
    private async Task PostAsync(CombatEntity combat, string content, CancellationToken ct)
    {
        var msg = new Message
        {
            SessionId = combat.SessionId,
            Content = content,
            Type = "Combat",
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Messages.Add(msg);
        await db.SaveChangesAsync(ct);
        await hub.Clients.Group(combat.GameId.ToString()).SendAsync("NewMessage",
            new MessageDto(msg.Id, msg.SessionId, null, content, msg.Type, false, msg.CreatedAt, new { combatId = combat.Id }), ct);
    }

    internal static IReadOnlyList<string> ReadConditions(JsonElement conditions)
        => conditions.ValueKind != JsonValueKind.Array
            ? []
            : conditions.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String)
                .Select(e => e.GetString()!)
                .ToList();

    private static DeathSaveStateDto? ReadDeathSaves(JsonElement state)
    {
        if (state.ValueKind != JsonValueKind.Object) return null;
        return new DeathSaveStateDto(
            state.TryGetProperty("successes", out var s) && s.TryGetInt32(out var sv) ? sv : 0,
            state.TryGetProperty("failures", out var f) && f.TryGetInt32(out var fv) ? fv : 0,
            state.TryGetProperty("isDead", out var d) && d.ValueKind == JsonValueKind.True,
            state.TryGetProperty("isStable", out var st) && st.ValueKind == JsonValueKind.True);
    }
}
