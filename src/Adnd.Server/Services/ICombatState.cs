using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Adnd.Server.Data;
using Adnd.Server.Models;

namespace Adnd.Server.Services;

/// <summary>
/// Service for managing combat state: HP, conditions, death saves, and rest.
/// Extracted from the monolithic CombatService to reduce partial file count.
/// </summary>
public interface ICombatStateService
{
    // HP Management
    Task<Combat> DealDamageAsync(Guid combatId, Guid participantId, int damage, string? source = null);
    Task<Combat> DealTemporaryHPAsync(Guid combatId, Guid participantId, int tempHP);
    Task<Combat> HealAsync(Guid combatId, Guid participantId, int amount, string? source = null);

    // Conditions
    Task<Combat> ApplyConditionAsync(Guid combatId, Guid participantId, string conditionName, int? duration = null, string? description = null);
    Task<Combat> RemoveConditionAsync(Guid combatId, Guid participantId, string conditionName);
    Task<Combat> ClearConditionsAsync(Guid combatId, Guid participantId, string? exceptCondition = null);

    // Death Saves
    Task<DeathSaveResult> MakeDeathSaveAsync(Guid combatId, Guid participantId, bool success);
    Task<Combat> AddDeathSaveSuccessAsync(Guid combatId, Guid participantId);
    Task<Combat> AddDeathSaveFailureAsync(Guid combatId, Guid participantId);

    // Rest
    Task<Combat> StartShortRestAsync(Guid combatId);
    Task<Combat> StartLongRestAsync(Guid combatId);
    Task<Combat> EndRestAsync(Guid combatId);
    Task<RestResult> GetCurrentRestStatusAsync(Guid combatId);
}

/// <summary>
/// Implementation of combat state management.
/// Uses ISystemRules for system-specific calculations.
/// </summary>
public class CombatStateService : ICombatStateService
{
    private readonly AppDbContext _context;
    private readonly IDiceEngine _diceEngine;
    private readonly ISystemRulesFactory _rulesFactory;
    private readonly ILogger<CombatStateService> _logger;

    public CombatStateService(AppDbContext context, IDiceEngine diceEngine,
        ISystemRulesFactory rulesFactory, ILogger<CombatStateService> logger)
    {
        _context = context;
        _diceEngine = diceEngine;
        _rulesFactory = rulesFactory;
        _logger = logger;
    }

    // ===== HP Management =====

    public async Task<Combat> DealDamageAsync(Guid combatId, Guid participantId, int damage, string? source = null)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant {participantId} not found in combat.");

        return await DealDamageToParticipant(combat, participant, damage, source ?? "unknown");
    }

    public async Task<Combat> DealTemporaryHPAsync(Guid combatId, Guid participantId, int tempHP)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant {participantId} not found in combat.");

        var currentTemp = participant.TemporaryHP?.GetInt32() ?? 0;
        participant.TemporaryHP = JsonDocument.Parse(JsonSerializer.Serialize(currentTemp + tempHP)).RootElement;

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.Healing, "System", participant.DisplayName,
            $"Gained {tempHP} temporary HP (total: {currentTemp + tempHP}).");

        return combat;
    }

    public async Task<Combat> HealAsync(Guid combatId, Guid participantId, int amount, string? source = null)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant {participantId} not found in combat.");

        var oldHP = participant.CurrentHP;
        participant.CurrentHP = Math.Min(participant.MaxHP, participant.CurrentHP + amount);
        var actualHeal = participant.CurrentHP - oldHP;

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.Healing, source ?? "System", participant.DisplayName,
            $"Healed {actualHeal} HP ({oldHP} -> {participant.CurrentHP}/{participant.MaxHP}).");

        return combat;
    }

    // ===== Conditions =====

    public async Task<Combat> ApplyConditionAsync(Guid combatId, Guid participantId, string conditionName, int? duration = null, string? description = null)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant {participantId} not found in combat.");

        var conditions = await GetConditions(participant);

        var existingIdx = conditions.FindIndex(c => c.Name.ToLower() == conditionName.ToLower());
        if (existingIdx >= 0)
        {
            if (duration.HasValue && duration.Value > 0)
            {
                conditions[existingIdx].Duration = duration.Value;
                conditions[existingIdx].Description = description;
            }
        }
        else
        {
            conditions.Add(new ConditionEntry
            {
                Name = conditionName,
                Duration = duration ?? 0,
                Description = description
            });
        }

        participant.Conditions = JsonDocument.Parse(JsonSerializer.Serialize(conditions)).RootElement;
        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.Condition, "System", participant.DisplayName,
            $"Applied '{conditionName}'{(duration.HasValue && duration.Value > 0 ? $" for {duration.Value} round(s)" : "")}.");

        return combat;
    }

    public async Task<Combat> RemoveConditionAsync(Guid combatId, Guid participantId, string conditionName)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant {participantId} not found in combat.");

        var conditions = await GetConditions(participant);
        var removed = conditions.RemoveAll(c => c.Name.ToLower() == conditionName.ToLower());

        if (removed > 0)
        {
            participant.Conditions = JsonDocument.Parse(JsonSerializer.Serialize(conditions)).RootElement;
            _context.Combats.Update(combat);
            await _context.SaveChangesAsync();

            await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
                CombatEventType.Condition, "System", participant.DisplayName,
                $"Removed '{conditionName}'.");
        }

        return combat;
    }

    public async Task<Combat> ClearConditionsAsync(Guid combatId, Guid participantId, string? exceptCondition = null)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant {participantId} not found in combat.");

        var conditions = await GetConditions(participant);
        var cleared = exceptCondition == null
            ? conditions.Count
            : conditions.RemoveAll(c => c.Name.ToLower() != exceptCondition.ToLower());

        if (cleared > 0)
        {
            participant.Conditions = JsonDocument.Parse(JsonSerializer.Serialize(conditions)).RootElement;
            _context.Combats.Update(combat);
            await _context.SaveChangesAsync();

            await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
                CombatEventType.Condition, "System", participant.DisplayName,
                $"Cleared {(cleared == conditions.Count + cleared ? "all conditions" : $"{cleared} conditions")}.");
        }

        return combat;
    }

    // ===== Death Saves =====

    public async Task<DeathSaveResult> MakeDeathSaveAsync(Guid combatId, Guid participantId, bool success)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant {participantId} not found in combat.");

        var deathState = GetDeathSaveState(participant);

        if (success)
        {
            deathState.Successes++;
            await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
                CombatEventType.DeathSave, participant.DisplayName, participant.DisplayName,
                $"Death save SUCCESS ({deathState.Successes}/3 successes).");

            if (deathState.Successes >= 3)
            {
                participant.CurrentHP = 1;
                participant.DeathSaveState = JsonDocument.Parse("{}").RootElement;
                await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
                    CombatEventType.Revival, "System", participant.DisplayName,
                    "Stabilized! Revived to 1 HP.");
            }
        }
        else
        {
            deathState.Failures++;
            await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
                CombatEventType.DeathSave, participant.DisplayName, participant.DisplayName,
                $"Death save FAILURE ({deathState.Failures}/3 failures).");

            if (deathState.Failures >= 3)
            {
                participant.CurrentHP = 0;
                participant.DeathSaveState = JsonDocument.Parse("{}").RootElement;
                await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
                    CombatEventType.Death, participant.DisplayName, participant.DisplayName,
                    "DIED from death saves!");
            }
        }

        participant.DeathSaveState = JsonDocument.Parse(JsonSerializer.Serialize(deathState)).RootElement;
        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();

        return new DeathSaveResult
        {
            Participant = participant.DisplayName,
            Success = success,
            Successes = deathState.Successes,
            Failures = deathState.Failures,
            IsStabilized = deathState.Successes >= 3,
            IsDead = deathState.Failures >= 3,
            RolledAt = DateTime.UtcNow
        };
    }

    public async Task<Combat> AddDeathSaveSuccessAsync(Guid combatId, Guid participantId)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant {participantId} not found in combat.");

        var deathState = GetDeathSaveState(participant);
        deathState.Successes++;
        participant.DeathSaveState = JsonDocument.Parse(JsonSerializer.Serialize(deathState)).RootElement;

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();

        if (deathState.Successes >= 3)
        {
            participant.CurrentHP = 1;
            participant.DeathSaveState = JsonDocument.Parse("{}").RootElement;
            await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
                CombatEventType.Revival, "System", participant.DisplayName,
                "Stabilized! Revived to 1 HP.");
        }

        return combat;
    }

    public async Task<Combat> AddDeathSaveFailureAsync(Guid combatId, Guid participantId)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant {participantId} not found in combat.");

        var deathState = GetDeathSaveState(participant);
        deathState.Failures++;
        participant.DeathSaveState = JsonDocument.Parse(JsonSerializer.Serialize(deathState)).RootElement;

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();

        if (deathState.Failures >= 3)
        {
            participant.CurrentHP = 0;
            participant.DeathSaveState = JsonDocument.Parse("{}").RootElement;
            await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
                CombatEventType.Death, participant.DisplayName, participant.DisplayName,
                "DIED from death saves!");
        }

        return combat;
    }

    // ===== Rest =====

    public async Task<Combat> StartShortRestAsync(Guid combatId)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        combat.Name = combat.Name + " (Short Rest)";

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.RoundStart, "System", "System",
            "Short rest started. Participants can recover HP and resources.");

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();
        return combat;
    }

    public async Task<Combat> StartLongRestAsync(Guid combatId)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        combat.Name = combat.Name + " (Long Rest)";

        foreach (var participant in combat.Participants)
        {
            if (participant.CurrentHP < participant.MaxHP)
            {
                var oldHP = participant.CurrentHP;
                participant.CurrentHP = participant.MaxHP;
                await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
                    CombatEventType.Healing, "System", participant.DisplayName,
                    $"Long rest recovery: {oldHP} -> {participant.CurrentHP} HP");
            }
        }

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.RoundStart, "System", "System",
            "Long rest completed. All HP recovered.");

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();
        return combat;
    }

    public async Task<Combat> EndRestAsync(Guid combatId)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.RoundStart, "System", "System",
            "Rest ended. Combat may resume.");

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();
        return combat;
    }

    public async Task<RestResult> GetCurrentRestStatusAsync(Guid combatId)
    {
        var combat = await GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        return new RestResult
        {
            RestType = combat.Name?.Contains("Short") == true ? "Short" :
                       combat.Name?.Contains("Long") == true ? "Long" : "None",
            IsInProgress = combat.Name?.Contains("Rest") == true,
            HPRecovered = combat.Participants.Sum(p => Math.Max(0, p.MaxHP - p.CurrentHP)),
            Effects = new List<string> { "HP recovery", "Resource recovery" }
        };
    }

    // ===== Helpers =====

    private async Task<Combat> DealDamageToParticipant(Combat combat, CombatParticipant participant, int damage, string source)
    {
        var tempHP = participant.TemporaryHP?.GetInt32() ?? 0;
        int damageToHP = damage;

        if (tempHP > 0)
        {
            if (damage <= tempHP)
            {
                participant.TemporaryHP = JsonDocument.Parse(JsonSerializer.Serialize(0)).RootElement;
                await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
                    CombatEventType.Damage, source, participant.DisplayName,
                    $"Took {damage} damage (absorbed by {damage} temp HP).");
                return combat;
            }
            else
            {
                damageToHP = damage - tempHP;
                participant.TemporaryHP = JsonDocument.Parse(JsonSerializer.Serialize(0)).RootElement;
            }
        }

        participant.CurrentHP = Math.Max(0, participant.CurrentHP - damageToHP);

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.Damage, source, participant.DisplayName,
            $"Took {damageToHP} damage ({participant.CurrentHP}/{participant.MaxHP} HP remaining).");

        if (participant.CurrentHP <= 0)
        {
            var ds = GetDeathSaveState(participant);
            if (ds.Successes == 0 && ds.Failures == 0)
            {
                participant.DeathSaveState = JsonDocument.Parse(JsonSerializer.Serialize(new DeathSaveState { Successes = 0, Failures = 0 })).RootElement;
            }
        }

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();
        return combat;
    }

    private async Task<List<ConditionEntry>> GetConditions(CombatParticipant participant)
    {
        if (participant.Conditions.ValueKind == JsonValueKind.Array)
        {
            return JsonSerializer.Deserialize<List<ConditionEntry>>(participant.Conditions.ToString())
                ?? new List<ConditionEntry>();
        }
        return new List<ConditionEntry>();
    }

    private DeathSaveState GetDeathSaveState(CombatParticipant participant)
    {
        if (participant.DeathSaveState.HasValue &&
            participant.DeathSaveState.Value.ValueKind == JsonValueKind.Object)
        {
            return JsonSerializer.Deserialize<DeathSaveState>(participant.DeathSaveState.ToString())
                ?? new DeathSaveState();
        }
        return new DeathSaveState();
    }

    private async Task AddCombatEvent(Combat combat, int round, int turnIndex, CombatEventType type,
        string actorName, string targetName, string content)
    {
        var evt = new CombatEvent
        {
            CombatId = combat.Id,
            Round = round,
            TurnIndex = turnIndex,
            Type = type,
            ActorName = actorName,
            TargetName = targetName,
            Content = content
        };
        _context.CombatEvents.Add(evt);
        await _context.SaveChangesAsync();
    }

    private async Task<Combat?> GetCombatWithParticipants(Guid combatId)
    {
        return await _context.Combats
            .Include(c => c.Participants)
            .Include(c => c.Events)
            .FirstOrDefaultAsync(c => c.Id == combatId);
    }

    private async Task<Combat?> GetCombatAsync(Guid combatId)
    {
        return await _context.Combats
            .Include(c => c.Participants)
            .Include(c => c.Events)
            .FirstOrDefaultAsync(c => c.Id == combatId);
    }
}
