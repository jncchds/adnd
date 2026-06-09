using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Adnd.Server.Data;
using Adnd.Server.Models;

namespace Adnd.Server.Services;

/// <summary>
/// Service for combat lifecycle: start, end, pause, resume.
/// </summary>
public interface ICombatLifecycleService
{
    Task<Combat> StartCombatAsync(Guid gameId, Guid? sessionId, string? name = null);
    Task<Combat> EndCombatAsync(Guid combatId, string? result = null);
    Task<Combat> PauseCombatAsync(Guid combatId);
    Task<Combat> ResumeCombatAsync(Guid combatId);
}

public class CombatLifecycleService : ICombatLifecycleService
{
    private readonly AppDbContext _context;
    private readonly ILogger<CombatLifecycleService> _logger;

    public CombatLifecycleService(AppDbContext context, ILogger<CombatLifecycleService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Combat> StartCombatAsync(Guid gameId, Guid? sessionId, string? name = null)
    {
        var combat = new Combat
        {
            GameId = gameId,
            SessionId = sessionId,
            Name = name,
            Status = CombatStatus.Active,
            CurrentRound = 1,
            CurrentTurnIndex = 0,
            Participants = new List<CombatParticipant>(),
            Events = new List<CombatEvent>()
        };

        _context.Combats.Add(combat);
        await _context.SaveChangesAsync();

        await AddCombatEvent(combat, 0, 0, CombatEventType.CombatStart, "System", "System",
            $"Combat '{name}' started.");

        _logger.LogInformation("Combat started: {CombatId} in game {GameId}", combat.Id, gameId);
        return combat;
    }

    public async Task<Combat> EndCombatAsync(Guid combatId, string? result = null)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        combat.Status = CombatStatus.Finished;
        combat.EndedAt = DateTime.UtcNow;

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.CombatEnd, "System", "System",
            result ?? "Combat ended.");

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Combat ended: {CombatId} with result: {Result}", combatId, result ?? "unknown");
        return combat;
    }

    public async Task<Combat> PauseCombatAsync(Guid combatId)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        combat.Status = CombatStatus.Paused;
        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();
        return combat;
    }

    public async Task<Combat> ResumeCombatAsync(Guid combatId)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        combat.Status = CombatStatus.Active;
        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();
        return combat;
    }

    private async Task<Combat?> GetCombatWithParticipants(Guid combatId)
    {
        return await _context.Combats
            .Include(c => c.Participants)
            .Include(c => c.Events)
            .FirstOrDefaultAsync(c => c.Id == combatId);
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
}

/// <summary>
/// Service for managing combat participants.
/// </summary>
public interface ICombatParticipantService
{
    Task<CombatParticipant> AddParticipantAsync(Guid combatId, string participantType,
        Guid? playerId, Guid? npcId, string displayName, int ac, int currentHP, int maxHP,
        JsonElement? conditions = null, JsonElement? savingThrows = null,
        JsonElement? deathSaveState = null);
    Task<Combat> RemoveParticipantAsync(Guid combatId, Guid participantId);
}

public class CombatParticipantService : ICombatParticipantService
{
    private readonly AppDbContext _context;
    private readonly ILogger<CombatParticipantService> _logger;

    public CombatParticipantService(AppDbContext context, ILogger<CombatParticipantService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<CombatParticipant> AddParticipantAsync(Guid combatId, string participantType,
        Guid? playerId, Guid? npcId, string displayName, int ac, int currentHP, int maxHP,
        JsonElement? conditions = null, JsonElement? savingThrows = null,
        JsonElement? deathSaveState = null)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        if (combat.Status != CombatStatus.Active)
            throw new InvalidOperationException("Cannot add participants to a finished combat.");

        var participant = new CombatParticipant
        {
            CombatId = combatId,
            ParticipantType = participantType,
            PlayerId = playerId,
            NpcId = npcId,
            DisplayName = displayName,
            AC = ac,
            CurrentHP = currentHP,
            MaxHP = maxHP,
            Conditions = conditions ?? JsonDocument.Parse("[]").RootElement,
            SavingThrows = savingThrows,
            DeathSaveState = deathSaveState,
            InitiativeCount = combat.InitiativeCount++
        };

        _context.CombatParticipants.Add(participant);
        await _context.SaveChangesAsync();

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.CombatStart, participant.DisplayName, participant.DisplayName,
            $"Added to combat (HP: {currentHP}/{maxHP}, AC: {ac}).");

        return participant;
    }

    public async Task<Combat> RemoveParticipantAsync(Guid combatId, Guid participantId)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId);
        if (participant == null)
            throw new KeyNotFoundException($"Participant {participantId} not found in combat.");

        _context.CombatParticipants.Remove(participant);
        await _context.SaveChangesAsync();

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.CombatEnd, participant.DisplayName, participant.DisplayName,
            "Removed from combat.");

        if (combat.CurrentTurnIndex >= combat.Participants.Count)
            combat.CurrentTurnIndex = Math.Max(0, combat.Participants.Count - 1);

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();
        return combat;
    }

    private async Task<Combat?> GetCombatWithParticipants(Guid combatId)
    {
        return await _context.Combats
            .Include(c => c.Participants)
            .Include(c => c.Events)
            .FirstOrDefaultAsync(c => c.Id == combatId);
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
}

/// <summary>
/// Service for initiative management.
/// </summary>
public interface ICombatInitiativeService
{
    Task<(CombatParticipant participant, int[] rolls)> RollInitiativeAsync(Guid combatId, Guid participantId, string formula = "1d20");
    Task<Combat> RollInitiativeForAllAsync(Guid combatId, string formula = "1d20");
    Task<Combat> ReorderInitiativeAsync(Guid combatId, List<Guid> participantIdsInOrder);
}

public class CombatInitiativeService : ICombatInitiativeService
{
    private readonly AppDbContext _context;
    private readonly IDiceEngine _diceEngine;
    private readonly ILogger<CombatInitiativeService> _logger;

    public CombatInitiativeService(AppDbContext context, IDiceEngine diceEngine, ILogger<CombatInitiativeService> logger)
    {
        _context = context;
        _diceEngine = diceEngine;
        _logger = logger;
    }

    public async Task<(CombatParticipant participant, int[] rolls)> RollInitiativeAsync(Guid combatId, Guid participantId, string formula = "1d20")
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant {participantId} not found in combat.");

        var result = _diceEngine.Roll(formula);
        participant.Initiative = result.Total;
        participant.InitiativeCount = combat.InitiativeCount++;

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.Initiative, participant.DisplayName, participant.DisplayName,
            $"Initiative: {formula} = {result.Total} ({string.Join(',', result.Rolls)})");

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();

        return (participant, result.Rolls);
    }

    public async Task<Combat> RollInitiativeForAllAsync(Guid combatId, string formula = "1d20")
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        foreach (var participant in combat.Participants)
        {
            var result = _diceEngine.Roll(formula);
            participant.Initiative = result.Total;
            participant.InitiativeCount = combat.InitiativeCount++;

            await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
                CombatEventType.Initiative, participant.DisplayName, participant.DisplayName,
                $"Initiative: {formula} = {result.Total} ({string.Join(',', result.Rolls)})");
        }

        combat.Participants = combat.Participants
            .OrderByDescending(p => p.Initiative)
            .ThenBy(p => p.InitiativeCount)
            .ToList();

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();

        var turnOrder = string.Join(", ", combat.Participants.Select(p => $"{p.DisplayName} ({p.Initiative})"));
        await AddCombatEvent(combat, combat.CurrentRound, 0, CombatEventType.RoundStart, "System", "System",
            $"Initiative order: {turnOrder}");

        return combat;
    }

    public async Task<Combat> ReorderInitiativeAsync(Guid combatId, List<Guid> participantIdsInOrder)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        combat.Participants = participantIdsInOrder
            .Select(id => combat.Participants.First(p => p.Id == id))
            .ToList();

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();
        return combat;
    }

    private async Task<Combat?> GetCombatWithParticipants(Guid combatId)
    {
        return await _context.Combats
            .Include(c => c.Participants)
            .Include(c => c.Events)
            .FirstOrDefaultAsync(c => c.Id == combatId);
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
}

/// <summary>
/// Service for turn management.
/// </summary>
public interface ICombatTurnService
{
    Task<Combat> AdvanceTurnAsync(Guid combatId);
    Task<Combat> RetreatTurnAsync(Guid combatId);
    Task<CombatParticipant> GetCurrentTurnParticipantAsync(Guid combatId);
    Task<Combat> SetCurrentTurnAsync(Guid combatId, Guid participantId);
}

public class CombatTurnService : ICombatTurnService
{
    private readonly AppDbContext _context;
    private readonly ILogger<CombatTurnService> _logger;

    public CombatTurnService(AppDbContext context, ILogger<CombatTurnService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Combat> AdvanceTurnAsync(Guid combatId)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        if (combat.Participants.Count == 0)
            throw new InvalidOperationException("No participants in combat.");

        var oldTurnIndex = combat.CurrentTurnIndex;
        combat.CurrentTurnIndex++;

        if (combat.CurrentTurnIndex >= combat.Participants.Count)
        {
            combat.CurrentRound++;
            combat.CurrentTurnIndex = 0;

            foreach (var participant in combat.Participants)
            {
                var conditions = await GetConditions(participant);
                var updatedConditions = new List<ConditionEntry>();
                foreach (var cond in conditions)
                {
                    if (cond.Duration > 0)
                    {
                        cond.Duration--;
                        if (cond.Duration > 0)
                            updatedConditions.Add(cond);
                        else
                        {
                            await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
                                CombatEventType.Condition, "System", participant.DisplayName,
                                $"Condition '{cond.Name}' expired.");
                        }
                    }
                }
                participant.Conditions = JsonDocument.Parse(JsonSerializer.Serialize(updatedConditions)).RootElement;
            }
        }

        var newTurnParticipant = combat.Participants[combat.CurrentTurnIndex];

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.TurnChange, "System", "System",
            $"Round {combat.CurrentRound}, Turn: {newTurnParticipant.DisplayName}");

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();
        return combat;
    }

    public async Task<Combat> RetreatTurnAsync(Guid combatId)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        if (combat.CurrentTurnIndex > 0)
        {
            combat.CurrentTurnIndex--;
        }
        else if (combat.CurrentRound > 1)
        {
            combat.CurrentRound--;
            combat.CurrentTurnIndex = combat.Participants.Count - 1;
        }

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();
        return combat;
    }

    public async Task<CombatParticipant> GetCurrentTurnParticipantAsync(Guid combatId)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        if (combat.Participants.Count == 0)
            throw new InvalidOperationException("No participants in combat.");

        return combat.Participants[combat.CurrentTurnIndex];
    }

    public async Task<Combat> SetCurrentTurnAsync(Guid combatId, Guid participantId)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var idx = combat.Participants.FindIndex(p => p.Id == participantId);
        if (idx < 0)
            throw new KeyNotFoundException($"Participant {participantId} not found in combat.");

        combat.CurrentTurnIndex = idx;
        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();
        return combat;
    }

    private async Task<Combat?> GetCombatWithParticipants(Guid combatId)
    {
        return await _context.Combats
            .Include(c => c.Participants)
            .Include(c => c.Events)
            .FirstOrDefaultAsync(c => c.Id == combatId);
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
}
