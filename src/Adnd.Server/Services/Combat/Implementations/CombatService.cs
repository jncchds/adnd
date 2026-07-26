using System.Text.Json;
using Adnd.Server.Data;
using Adnd.Server.Events;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Services.Combat;

/// <summary>
/// Facade over all 12 domain combat services.
/// Convenience methods (damage, healing, conditions, death saves) are implemented
/// here with full event logging and Wolverine event publishing.
/// </summary>
public sealed class CombatService(
    ICombatLifecycleService lifecycle,
    ICombatParticipantService participants,
    ICombatInitiativeService initiative,
    ICombatTurnService turns,
    ICombatStateService state,
    ICombatSpellService spells,
    ICombatInventoryService inventory,
    ICombatProgressionService progression,
    ICombatGridService grid,
    ICombatAIService ai,
    ICombatQueryService query,
    ISANService san,
    AppDbContext db,
    IEventBus eventBus,
    CombatEventLogger logger) : ICombatService
{
    public ICombatLifecycleService Lifecycle => lifecycle;
    public ICombatParticipantService Participants => participants;
    public ICombatInitiativeService Initiative => initiative;
    public ICombatTurnService Turns => turns;
    public ICombatStateService State => state;
    public ICombatSpellService Spells => spells;
    public ICombatInventoryService Inventory => inventory;
    public ICombatProgressionService Progression => progression;
    public ICombatGridService Grid => grid;
    public ICombatAIService AI => ai;
    public ICombatQueryService Query => query;
    public ISANService SAN => san;

    // ── Damage ────────────────────────────────────────────────────────────────

    public async Task<DamageResult> DealDamageAsync(
        Guid participantId,
        int amount,
        string damageType,
        CancellationToken ct = default)
    {
        var participant = await db.CombatParticipants
            .Include(p => p.Combat)
            .FirstOrDefaultAsync(p => p.Id == participantId, ct)
            ?? throw new InvalidOperationException($"Participant {participantId} not found.");

        var damage = Math.Max(0, amount);
        participant.HP = Math.Max(0, participant.HP - damage);
        var isDown = participant.HP <= 0;

        logger.Log(
            participant.Combat,
            CombatEventType.Damage,
            null,
            participantId,
            $"{damageType} damage: {damage}",
            new { damage, damageType, newHP = participant.HP, isDown });

        await db.SaveChangesAsync(ct);

        await eventBus.PublishAsync(
            new CombatDamageDealt(participant.Combat.GameId, participant.CombatId, participantId, damage, damageType),
            ct);

        if (isDown)
            await eventBus.PublishAsync(
                new CombatConditionApplied(participant.Combat.GameId, participant.CombatId, participantId, "Down"),
                ct);

        return new DamageResult(participantId, damage, participant.HP, isDown);
    }

    // ── Healing ───────────────────────────────────────────────────────────────

    public async Task HealAsync(Guid participantId, int amount, CancellationToken ct = default)
    {
        var participant = await db.CombatParticipants
            .Include(p => p.Combat)
            .FirstOrDefaultAsync(p => p.Id == participantId, ct)
            ?? throw new InvalidOperationException($"Participant {participantId} not found.");

        var healed = Math.Max(0, amount);
        participant.HP = Math.Min(participant.MaxHP, participant.HP + healed);

        logger.Log(
            participant.Combat,
            CombatEventType.Healing,
            null,
            participantId,
            $"Healed: {healed}",
            new { healed, newHP = participant.HP });

        await db.SaveChangesAsync(ct);

        await eventBus.PublishAsync(
            new CombatHealed(participant.Combat.GameId, participant.CombatId, participantId, healed),
            ct);
    }

    // ── Conditions ────────────────────────────────────────────────────────────

    public async Task ApplyConditionAsync(Guid participantId, string condition, CancellationToken ct = default)
    {
        var participant = await db.CombatParticipants
            .Include(p => p.Combat)
            .FirstOrDefaultAsync(p => p.Id == participantId, ct)
            ?? throw new InvalidOperationException($"Participant {participantId} not found.");

        var conditions = DeserializeConditions(participant.Conditions);
        conditions[condition] = condition;
        participant.Conditions = SerializeConditions(conditions);

        logger.Log(
            participant.Combat,
            CombatEventType.Condition,
            null,
            participantId,
            $"Condition applied: {condition}");

        await db.SaveChangesAsync(ct);

        await eventBus.PublishAsync(
            new CombatConditionApplied(participant.Combat.GameId, participant.CombatId, participantId, condition),
            ct);
    }

    public async Task RemoveConditionAsync(Guid participantId, string condition, CancellationToken ct = default)
    {
        var participant = await db.CombatParticipants
            .Include(p => p.Combat)
            .FirstOrDefaultAsync(p => p.Id == participantId, ct)
            ?? throw new InvalidOperationException($"Participant {participantId} not found.");

        var conditions = DeserializeConditions(participant.Conditions);
        conditions.Remove(condition);
        participant.Conditions = SerializeConditions(conditions);

        await db.SaveChangesAsync(ct);

        await eventBus.PublishAsync(
            new CombatConditionRemoved(participant.Combat.GameId, participant.CombatId, participantId, condition),
            ct);
    }

    // ── Death Saves (D&D 5e exact) ────────────────────────────────────────────

    public async Task<DeathSaveResult> RecordDeathSaveAsync(
        Guid participantId,
        bool isSuccess,
        int roll,
        CancellationToken ct = default)
    {
        var participant = await db.CombatParticipants
            .Include(p => p.Combat)
            .FirstOrDefaultAsync(p => p.Id == participantId, ct)
            ?? throw new InvalidOperationException($"Participant {participantId} not found.");

        var saves = DeserializeDeathSaves(participant.DeathSaveState);
        bool revived = false, stable = false, dead = false;

        if (roll == 20)
        {
            // Natural 20 → revive at 1 HP, clear state
            participant.HP = 1;
            saves = new DeathSaves(0, 0);
            revived = true;
        }
        else if (roll == 1)
        {
            // Natural 1 → 2 failures
            saves = saves with { Failures = saves.Failures + 2 };
        }
        else if (isSuccess)
        {
            saves = saves with { Successes = saves.Successes + 1 };
        }
        else
        {
            saves = saves with { Failures = saves.Failures + 1 };
        }

        if (saves.Successes >= 3)
            stable = true;

        if (saves.Failures >= 3)
            dead = true;

        participant.DeathSaveState = JsonSerializer.Deserialize<JsonElement>(
            JsonSerializer.Serialize(new { successes = saves.Successes, failures = saves.Failures }));

        logger.Log(
            participant.Combat,
            CombatEventType.DeathSave,
            participantId,
            null,
            $"Death save roll {roll}: {(revived ? "revived" : dead ? "dead" : stable ? "stable" : isSuccess ? "success" : "failure")}",
            new { roll, revived, stable, dead, successes = saves.Successes, failures = saves.Failures });

        await db.SaveChangesAsync(ct);

        if (dead)
            await eventBus.PublishAsync(
                new CombatConditionApplied(participant.Combat.GameId, participant.CombatId, participantId, "Dead"),
                ct);

        return new DeathSaveResult(participantId, stable || revived, dead, revived, saves.Successes, saves.Failures);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private record DeathSaves(int Successes, int Failures);

    private static DeathSaves DeserializeDeathSaves(JsonElement el)
    {
        if (el.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            return new DeathSaves(0, 0);

        var successes = el.TryGetProperty("successes", out var s) ? s.GetInt32() : 0;
        var failures = el.TryGetProperty("failures", out var f) ? f.GetInt32() : 0;
        return new DeathSaves(successes, failures);
    }

    private static Dictionary<string, string> DeserializeConditions(JsonElement el)
    {
        if (el.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            return new();

        return JsonSerializer.Deserialize<Dictionary<string, string>>(el.GetRawText()) ?? new();
    }

    private static JsonElement SerializeConditions(Dictionary<string, string> conditions)
        => JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(conditions));
}
