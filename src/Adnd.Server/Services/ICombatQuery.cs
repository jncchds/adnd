using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Adnd.Server.Data;
using Adnd.Server.Models;

namespace Adnd.Server.Services;

/// <summary>
/// Service for querying combat data (read-only operations).
/// </summary>
public interface ICombatQueryService
{
    Task<CombatLog> GetCombatLogAsync(Guid combatId);
    Task<Combat?> GetCombatAsync(Guid combatId);
    Task<List<Combat>> GetActiveCombatAsync(Guid gameId);
}

public class CombatQueryService : ICombatQueryService
{
    private readonly AppDbContext _context;

    public CombatQueryService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<CombatLog> GetCombatLogAsync(Guid combatId)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var events = await _context.CombatEvents
            .Where(e => e.CombatId == combatId)
            .OrderBy(e => e.CreatedAt)
            .ToListAsync();

        return new CombatLog
        {
            CombatId = combatId,
            Name = combat.Name,
            Status = combat.Status,
            CurrentRound = combat.CurrentRound,
            CurrentTurnIndex = combat.CurrentTurnIndex,
            Participants = combat.Participants
                .OrderBy(p => p.Initiative)
                .ThenByDescending(p => p.InitiativeCount)
                .Select(p => new ParticipantSummary
                {
                    Id = p.Id,
                    DisplayName = p.DisplayName,
                    ParticipantType = p.ParticipantType,
                    CurrentHP = p.CurrentHP,
                    MaxHP = p.MaxHP,
                    AC = p.AC,
                    Initiative = p.Initiative,
                    Conditions = JsonSerializer.Deserialize<List<ConditionEntry>>(p.Conditions.ToString()) ?? new(),
                    IsCurrentTurn = p.Id == combat.Participants[Math.Min(combat.CurrentTurnIndex, combat.Participants.Count - 1)].Id,
                    IsDead = p.CurrentHP <= 0
                })
                .ToList(),
            Events = events.Select(e => new CombatLogEvent
            {
                Id = e.Id,
                Round = e.Round,
                TurnIndex = e.TurnIndex,
                Type = e.Type,
                ActorName = e.ActorName,
                TargetName = e.TargetName,
                Content = e.Content,
                Metadata = e.Metadata,
                CreatedAt = e.CreatedAt
            }).ToList()
        };
    }

    public async Task<Combat?> GetCombatAsync(Guid combatId)
    {
        return await _context.Combats
            .Include(c => c.Participants)
            .Include(c => c.Events)
            .FirstOrDefaultAsync(c => c.Id == combatId);
    }

    public async Task<List<Combat>> GetActiveCombatAsync(Guid gameId)
    {
        return await _context.Combats
            .Include(c => c.Participants)
            .Where(c => c.GameId == gameId && c.Status == CombatStatus.Active)
            .OrderByDescending(c => c.StartedAt)
            .ToListAsync();
    }

    private async Task<Combat?> GetCombatWithParticipants(Guid combatId)
    {
        return await _context.Combats
            .Include(c => c.Participants)
            .Include(c => c.Events)
            .FirstOrDefaultAsync(c => c.Id == combatId);
    }
}

/// <summary>
/// Service for SAN (Call of Cthulhu specific) management.
/// </summary>
public interface ISANService
{
    Task<Combat> ApplySANLossAsync(Guid combatId, Guid participantId, int sanLoss, string reason);
    Task<Combat> ApplySANRecoveryAsync(Guid combatId, Guid participantId, int sanRecovery);
    Task<SanityCheckResult> MakeSANCheckAsync(Guid combatId, Guid participantId, int dc);
}

public class SANService : ISANService
{
    private readonly AppDbContext _context;
    private readonly IDiceEngine _diceEngine;
    private readonly ILogger<SANService> _logger;

    public SANService(AppDbContext context, IDiceEngine diceEngine, ILogger<SANService> logger)
    {
        _context = context;
        _diceEngine = diceEngine;
        _logger = logger;
    }

    public async Task<Combat> ApplySANLossAsync(Guid combatId, Guid participantId, int sanLoss, string reason)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant {participantId} not found in combat.");

        var notes = GetNotes(participant);
        if (notes == null) notes = new Dictionary<string, object>();

        if (!notes.ContainsKey("currentSAN")) notes["currentSAN"] = 60;
        if (!notes.ContainsKey("maxSAN")) notes["maxSAN"] = 60;

        var currentSAN = Convert.ToInt32(notes["currentSAN"]);
        var maxSAN = Convert.ToInt32(notes["maxSAN"]);
        var newSAN = Math.Max(0, currentSAN - sanLoss);
        notes["currentSAN"] = newSAN;

        participant.Notes = JsonDocument.Parse(JsonSerializer.Serialize(notes)).RootElement;

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.Condition, "System", participant.DisplayName,
            $"SAN loss: {currentSAN} -> {newSAN} ({sanLoss} lost) - {reason}");

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();
        return combat;
    }

    public async Task<Combat> ApplySANRecoveryAsync(Guid combatId, Guid participantId, int sanRecovery)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant {participantId} not found in combat.");

        var notes = GetNotes(participant);
        if (notes == null) notes = new Dictionary<string, object>();

        if (!notes.ContainsKey("currentSAN")) notes["currentSAN"] = 60;
        if (!notes.ContainsKey("maxSAN")) notes["maxSAN"] = 60;

        var currentSAN = Convert.ToInt32(notes["currentSAN"]);
        var maxSAN = Convert.ToInt32(notes["maxSAN"]);
        var newSAN = Math.Min(maxSAN, currentSAN + sanRecovery);
        notes["currentSAN"] = newSAN;

        participant.Notes = JsonDocument.Parse(JsonSerializer.Serialize(notes)).RootElement;

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.Healing, "System", participant.DisplayName,
            $"SAN recovery: {currentSAN} -> {newSAN} (+{sanRecovery})");

        _context.Combats.Update(combat);
        await _context.SaveChangesAsync();
        return combat;
    }

    public async Task<SanityCheckResult> MakeSANCheckAsync(Guid combatId, Guid participantId, int dc)
    {
        var combat = await GetCombatWithParticipants(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant {participantId} not found in combat.");

        var notes = GetNotes(participant);
        if (notes == null) notes = new Dictionary<string, object>();

        if (!notes.ContainsKey("currentSAN")) notes["currentSAN"] = 60;
        if (!notes.ContainsKey("maxSAN")) notes["maxSAN"] = 60;

        var currentSAN = Convert.ToInt32(notes["currentSAN"]);
        var maxSAN = Convert.ToInt32(notes["maxSAN"]);

        var rollResult = _diceEngine.Roll("d100");
        var roll = rollResult.Total;
        var success = roll <= currentSAN;
        var isCritical = roll <= (currentSAN / 5);
        var isFumble = roll >= 95;

        int sanLoss = 0;
        string effect = string.Empty;

        if (success)
        {
            if (isCritical)
            {
                sanLoss = 0;
                effect = "Critical success - no SAN loss";
            }
            else if (isFumble)
            {
                var fumbleDmg = _diceEngine.Roll("1d10");
                sanLoss = fumbleDmg.Total;
                effect = $"Fumble! Lost {sanLoss} SAN";
            }
            else
            {
                sanLoss = 0;
                effect = "Success - no SAN loss";
            }
        }
        else
        {
            var sanDmg = _diceEngine.Roll("1d10");
            sanLoss = sanDmg.Total;
            effect = $"Failed - lost {sanLoss} SAN";
        }

        var newSAN = Math.Max(0, currentSAN - sanLoss);
        notes["currentSAN"] = newSAN;
        participant.Notes = JsonDocument.Parse(JsonSerializer.Serialize(notes)).RootElement;

        await AddCombatEvent(combat, combat.CurrentRound, combat.CurrentTurnIndex,
            CombatEventType.SaveThrow, participant.DisplayName, participant.DisplayName,
            $"SAN check: d100={roll} vs {currentSAN} -> {(success ? "SUCCESS" : "FAILURE")} | SAN: {currentSAN} -> {newSAN} (-{sanLoss})");

        return new SanityCheckResult
        {
            Participant = participant.DisplayName,
            CurrentSAN = currentSAN,
            Roll = roll,
            DC = dc,
            Success = success,
            IsCritical = isCritical,
            SANLoss = sanLoss,
            Effect = effect,
            RolledAt = DateTime.UtcNow
        };
    }

    private async Task<Combat?> GetCombatWithParticipants(Guid combatId)
    {
        return await _context.Combats
            .Include(c => c.Participants)
            .Include(c => c.Events)
            .FirstOrDefaultAsync(c => c.Id == combatId);
    }

    private Dictionary<string, object>? GetNotes(CombatParticipant participant)
    {
        if (participant.Notes.HasValue &&
            participant.Notes.Value.ValueKind == JsonValueKind.Object)
        {
            return JsonSerializer.Deserialize<Dictionary<string, object>>(participant.Notes.ToString());
        }
        return null;
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
