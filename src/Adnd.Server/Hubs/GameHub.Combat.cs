using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Adnd.Server.Events;
using MediatR;
using System.Text.Json;

namespace Adnd.Server.Hubs;

public partial class GameHub
{
    // ==================== Combat ====================

    public async Task<CombatLogResponse> StartCombat(Guid gameId, Guid? sessionId, string? name = null)
    {
        var userId = Context.UserIdentifier;
        if (!string.IsNullOrEmpty(userId) && Guid.TryParse(userId, out var uid))
        {
            var player = await _context.Players
                .FirstOrDefaultAsync(p => p.UserId == uid && p.GameId == gameId);
            if (player == null || player.Role != PlayerRole.Creator)
                throw new ForbiddenException("Only the GM can start combat.");
        }

        var combat = await _combatService.StartCombatAsync(gameId, sessionId, name);

        // Publish event for game agent processing
        await _mediator.Publish(new CombatStarted(gameId, sessionId, name));

        await Clients.Group(gameId.ToString()).SendAsync("CombatStarted", new
        {
            combat.Id,
            combat.Name,
            combat.CurrentRound,
            Participants = combat.Participants.Select(p => new { p.Id, p.DisplayName, p.ParticipantType, p.CurrentHP, p.MaxHP, p.AC, p.Initiative }).ToList(),
            combat.StartedAt,
            FlavorText = $"Combat begins: {combat.Name ?? "An unexpected encounter!"}"
        });

        return BuildCombatLog(combat);
    }

    public async Task<CombatLogResponse> EndCombat(Guid combatId, string? result = null)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var endedCombat = await _combatService.EndCombatAsync(combatId, result);

        // Publish event for game agent processing
        await _mediator.Publish(new CombatEnded(combat.GameId, combatId, result));

        await Clients.Group(combat.GameId.ToString()).SendAsync("CombatEnded", new
        {
            combatId,
            result,
            EndedAt = DateTime.UtcNow,
            FlavorText = result ?? "The combat ends."
        });

        return BuildCombatLog(endedCombat);
    }

    public async Task<CombatLogResponse> PauseCombat(Guid combatId)
    {
        var combat = await _combatService.PauseCombatAsync(combatId);
        await Clients.Group(combat.GameId.ToString()).SendAsync("CombatPaused", new { combatId });
        return BuildCombatLog(combat);
    }

    public async Task<CombatLogResponse> ResumeCombat(Guid combatId)
    {
        var combat = await _combatService.ResumeCombatAsync(combatId);
        await Clients.Group(combat.GameId.ToString()).SendAsync("CombatResumed", new { combatId });
        return BuildCombatLog(combat);
    }

    public async Task<CombatParticipantResponse> AddParticipant(Guid combatId, string participantType,
        string displayName, int ac, int currentHP, int maxHP, Guid? playerId = null, Guid? npcId = null)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = await _combatService.AddParticipantAsync(
            combatId, participantType, playerId, npcId, displayName, ac, currentHP, maxHP);

        // Publish event for game agent processing
        await _mediator.Publish(new ParticipantAdded(
            combat.GameId, combatId, participantType, displayName, ac, currentHP, maxHP, playerId, npcId));

        await Clients.Group(combat.GameId.ToString()).SendAsync("CombatParticipantAdded", new
        {
            participant.Id,
            participant.DisplayName,
            participant.ParticipantType,
            participant.CurrentHP,
            participant.MaxHP,
            participant.AC,
            participant.Initiative
        });

        return BuildParticipantResponse(participant);
    }

    public async Task<CombatLogResponse> RemoveParticipant(Guid combatId, Guid participantId)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var result = await _combatService.RemoveParticipantAsync(combatId, participantId);
        await Clients.Group(combat.GameId.ToString()).SendAsync("CombatParticipantRemoved", new { participantId });
        return BuildCombatLog(result);
    }

    public async Task<(CombatParticipantResponse participant, int[] rolls)> RollInitiative(Guid combatId, Guid participantId, string formula = "1d20")
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var (participant, rolls) = await _combatService.RollInitiativeAsync(combatId, participantId, formula);

        await Clients.Group(combat.GameId.ToString()).SendAsync("InitiativeRolled", new
        {
            participant.Id,
            participant.DisplayName,
            participant.Initiative,
            rolls,
            FlavorText = $"{participant.DisplayName} rolls initiative: {participant.Initiative}"
        });

        return (BuildParticipantResponse(participant), rolls);
    }

    public async Task<CombatLogResponse> RollInitiativeForAll(Guid combatId, string formula = "1d20")
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var result = await _combatService.RollInitiativeForAllAsync(combatId, formula);

        var turnOrder = string.Join(", ", result.Participants.Select(p => $"{p.DisplayName} ({p.Initiative})"));
        await Clients.Group(combat.GameId.ToString()).SendAsync("InitiativeComplete", new
        {
            turnOrder,
            Participants = result.Participants.Select(p => new
            {
                p.Id,
                p.DisplayName,
                p.Initiative,
                p.CurrentHP,
                p.MaxHP,
                p.AC
            }),
            FlavorText = $"Initiative order: {turnOrder}"
        });

        return BuildCombatLog(result);
    }

    public async Task<CombatLogResponse> AdvanceTurn(Guid combatId)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var result = await _combatService.AdvanceTurnAsync(combatId);
        var currentTurn = await _combatService.GetCurrentTurnParticipantAsync(combatId);

        await Clients.Group(combat.GameId.ToString()).SendAsync("TurnAdvanced", new
        {
            result.CurrentRound,
            currentTurn.Id,
            currentTurn.DisplayName,
            currentTurn.CurrentHP,
            currentTurn.MaxHP,
            currentTurn.AC,
            currentTurn.Initiative
        });

        return BuildCombatLog(result);
    }

    public async Task<CombatLogResponse> RetreatTurn(Guid combatId)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var result = await _combatService.RetreatTurnAsync(combatId);
        var currentTurn = await _combatService.GetCurrentTurnParticipantAsync(combatId);

        await Clients.Group(combat.GameId.ToString()).SendAsync("TurnRetreated", new
        {
            result.CurrentRound,
            currentTurn.Id,
            currentTurn.DisplayName
        });

        return BuildCombatLog(result);
    }

    public async Task<CombatParticipantResponse> GetCurrentTurn(Guid combatId)
    {
        var participant = await _combatService.GetCurrentTurnParticipantAsync(combatId);
        return BuildParticipantResponse(participant);
    }

    public async Task<CombatLogResponse> SetCurrentTurn(Guid combatId, Guid participantId)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var result = await _combatService.SetCurrentTurnAsync(combatId, participantId);
        var currentTurn = await _combatService.GetCurrentTurnParticipantAsync(combatId);

        await Clients.Group(combat.GameId.ToString()).SendAsync("TurnSet", new
        {
            result.CurrentRound,
            currentTurn.Id,
            currentTurn.DisplayName
        });

        return BuildCombatLog(result);
    }

    public async Task<CombatAttackResponse> CombatAttack(Guid combatId, string attackerName, string weapon,
        Guid targetId, string attackFormula, int? attackBonus = null, string? damageFormula = null,
        int? damageBonus = null, string? description = null)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var result = await _combatService.ExecuteAttackAsync(
            combatId, attackerName, weapon, targetId, attackFormula,
            attackBonus, damageFormula, damageBonus, description);

        // Update target HP on the character sheet if it's a player
        if (result.TargetHP != result.TargetMaxHP)
        {
            var participant = combat.Participants.FirstOrDefault(p => p.Id == targetId);
            if (participant?.PlayerId.HasValue == true)
            {
                var player = await _context.Players.FindAsync(participant.PlayerId.Value);
                if (player?.Character != null)
                {
                    player.Character.CurrentHP = result.TargetHP;
                    _context.Characters.Update(player.Character);
                    await _context.SaveChangesAsync();
                }
            }
        }

        await Clients.Group(combat.GameId.ToString()).SendAsync("CombatAttack", new
        {
            result.Attacker,
            result.Weapon,
            result.Target,
            result.Hit,
            result.IsCritical,
            result.IsFumble,
            result.AttackRoll,
            result.AttackDice,
            result.AC,
            result.DamageDice,
            result.DamageTotal,
            result.DamageInfo,
            result.TargetHP,
            result.TargetMaxHP
        });

        return new CombatAttackResponse
        {
            Attacker = result.Attacker,
            Weapon = result.Weapon,
            Target = result.Target,
            Hit = result.Hit,
            IsCritical = result.IsCritical,
            IsFumble = result.IsFumble,
            AttackRoll = result.AttackRoll,
            AttackDice = result.AttackDice,
            AC = result.AC,
            DamageDice = result.DamageDice,
            DamageTotal = result.DamageTotal,
            DamageInfo = result.DamageInfo,
            TargetHP = result.TargetHP,
            TargetMaxHP = result.TargetMaxHP
        };
    }

    public async Task<CombatSaveThrowResponse> CombatSaveThrow(Guid combatId, string participantName,
        Guid participantId, string saveType, string saveFormula, int dc)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var result = await _combatService.ExecuteSaveThrowAsync(
            combatId, participantName, participantId, saveType, saveFormula, dc);

        await Clients.Group(combat.GameId.ToString()).SendAsync("CombatSaveThrow", new
        {
            result.Participant,
            result.SaveType,
            result.DiceRoll,
            result.DC,
            result.Success,
            result.RolledAt
        });

        return new CombatSaveThrowResponse
        {
            Participant = result.Participant,
            SaveType = result.SaveType,
            DiceRoll = result.DiceRoll,
            DC = result.DC,
            Success = result.Success
        };
    }

    public async Task<CombatLogResponse> CombatApplyCondition(Guid combatId, Guid participantId,
        string conditionName, int? duration = null, string? description = null)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var result = await _combatService.ApplyConditionAsync(
            combatId, participantId, conditionName, duration, description);

        await Clients.Group(combat.GameId.ToString()).SendAsync("ConditionApplied", new
        {
            participantId,
            conditionName,
            duration
        });

        return BuildCombatLog(result);
    }

    public async Task<CombatLogResponse> CombatRemoveCondition(Guid combatId, Guid participantId, string conditionName)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var result = await _combatService.RemoveConditionAsync(combatId, participantId, conditionName);

        await Clients.Group(combat.GameId.ToString()).SendAsync("ConditionRemoved", new
        {
            participantId,
            conditionName
        });

        return BuildCombatLog(result);
    }

    public async Task<CombatLogResponse> CombatDealDamage(Guid combatId, Guid participantId, int damage, string? source = null)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var result = await _combatService.DealDamageAsync(combatId, participantId, damage, source);

        // Update character HP if it's a player
        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId);
        if (participant?.PlayerId.HasValue == true)
        {
            var player = await _context.Players.FindAsync(participant.PlayerId.Value);
            if (player?.Character != null)
            {
                player.Character.CurrentHP = participant.CurrentHP;
                _context.Characters.Update(player.Character);
                await _context.SaveChangesAsync();
            }
        }

        await Clients.Group(combat.GameId.ToString()).SendAsync("CombatDamage", new
        {
            ParticipantId = participantId,
            DisplayName = participant?.DisplayName,
            Damage = damage,
            HP = participant?.CurrentHP,
            MaxHP = participant?.MaxHP,
            Source = source
        });

        return BuildCombatLog(result);
    }

    public async Task<CombatLogResponse> CombatHeal(Guid combatId, Guid participantId, int amount, string? source = null)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var result = await _combatService.HealAsync(combatId, participantId, amount, source);

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId);
        if (participant?.PlayerId.HasValue == true)
        {
            var player = await _context.Players.FindAsync(participant.PlayerId.Value);
            if (player?.Character != null)
            {
                player.Character.CurrentHP = participant.CurrentHP;
                _context.Characters.Update(player.Character);
                await _context.SaveChangesAsync();
            }
        }

        await Clients.Group(combat.GameId.ToString()).SendAsync("CombatHeal", new
        {
            ParticipantId = participantId,
            DisplayName = participant?.DisplayName,
            Amount = amount,
            HP = participant?.CurrentHP,
            MaxHP = participant?.MaxHP,
            Source = source,
            FlavorText = $"{participant?.DisplayName ?? "Someone"} heals {amount} HP from {source ?? "a mysterious source"}"
        });

        return BuildCombatLog(result);
    }

    public async Task<CombatDeathSaveResponse> CombatDeathSave(Guid combatId, Guid participantId, bool success)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var participant = combat.Participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new KeyNotFoundException($"Participant {participantId} not found.");

        var result = await _combatService.MakeDeathSaveAsync(combatId, participantId, success);

        await Clients.Group(combat.GameId.ToString()).SendAsync("CombatDeathSave", new
        {
            Participant = result.Participant,
            Success = result.Success,
            Successes = result.Successes,
            Failures = result.Failures,
            IsStabilized = result.IsStabilized,
            IsDead = result.IsDead
        });

        return new CombatDeathSaveResponse
        {
            Participant = result.Participant,
            Success = result.Success,
            Successes = result.Successes,
            Failures = result.Failures,
            IsStabilized = result.IsStabilized,
            IsDead = result.IsDead
        };
    }

    public async Task<CombatLogResponse> GetCombatLog(Guid combatId)
    {
        var combat = await _combatService.GetCombatAsync(combatId)
            ?? throw new KeyNotFoundException($"Combat {combatId} not found.");

        var log = await _combatService.GetCombatLogAsync(combatId);

        await Clients.Group(combat.GameId.ToString()).SendAsync("CombatLogUpdated", BuildCombatLogResponse(log));

        return BuildCombatLogResponse(log);
    }

    public async Task<List<CombatSummary>> GetActiveCombats(Guid gameId)
    {
        var combats = await _combatService.GetActiveCombatAsync(gameId);
        return combats.Select(c => new CombatSummary
        {
            Id = c.Id,
            Name = c.Name,
            Status = c.Status.ToString(),
            CurrentRound = c.CurrentRound,
            ParticipantCount = c.Participants.Count,
            StartedAt = c.StartedAt
        }).ToList();
    }

}
