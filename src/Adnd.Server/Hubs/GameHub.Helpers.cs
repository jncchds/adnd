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
    // ==================== Helpers ====================

    /// <summary>
    /// Get the SignalR connection ID for a specific player.
    /// Uses the in-memory player-to-connection mapping.
    /// </summary>
    private string? GetConnectionIdForPlayer(Guid playerId)
    {
        if (_playerConnections.TryGetValue(playerId.ToString(), out var connectionId))
        {
            return connectionId;
        }

        return null;
    }

    /// <summary>
    /// Builds a normalized whisper response object for SignalR broadcasting.
    /// </summary>
    private static object BuildWhisperResponse(Whisper w, string fromCharacter, PlayerRole fromRole) =>
        new
        {
            w.Id,
            FromPlayerId = w.FromPlayerId,
            FromCharacter = fromCharacter,
            FromRole = fromRole,
            Content = w.Content,
            Type = w.Type,
            Targets = w.Targets,
            CreatedAt = w.CreatedAt
        };

    // ==================== Combat Helpers ====================

    private static CombatLogResponse BuildCombatLog(Models.Combat combat)
    {
        var currentTurnId = combat.Participants.Count > 0
            ? combat.Participants[Math.Min(combat.CurrentTurnIndex, combat.Participants.Count - 1)].Id
            : Guid.Empty;

        return new CombatLogResponse
        {
            CombatId = combat.Id,
            Name = combat.Name,
            Status = combat.Status.ToString(),
            CurrentRound = combat.CurrentRound,
            CurrentTurnIndex = combat.CurrentTurnIndex,
            Participants = combat.Participants
                .OrderBy(p => p.Initiative)
                .ThenByDescending(p => p.InitiativeCount)
                .Select(p => new CombatParticipantResponse
                {
                    Id = p.Id,
                    DisplayName = p.DisplayName,
                    ParticipantType = p.ParticipantType,
                    CurrentHP = p.CurrentHP,
                    MaxHP = p.MaxHP,
                    AC = p.AC,
                    Initiative = p.Initiative,
                    Conditions = JsonSerializer.Deserialize<List<ConditionEntryResponse>>(p.Conditions.ToString()) ?? new(),
                    IsCurrentTurn = p.Id == currentTurnId,
                    IsDead = p.CurrentHP <= 0
                }).ToList(),
            Events = combat.Events
                .OrderBy(e => e.CreatedAt)
                .Select(e => new CombatLogEventResponse
                {
                    Id = e.Id,
                    Round = e.Round,
                    TurnIndex = e.TurnIndex,
                    Type = e.Type.ToString(),
                    ActorName = e.ActorName,
                    TargetName = e.TargetName,
                    Content = e.Content,
                    CreatedAt = e.CreatedAt
                }).ToList()
        };
    }

    private static CombatLogResponse BuildCombatLogResponse(CombatLog log)
    {
        return new CombatLogResponse
        {
            CombatId = log.CombatId,
            Name = log.Name,
            Status = log.Status.ToString(),
            CurrentRound = log.CurrentRound,
            CurrentTurnIndex = log.CurrentTurnIndex,
            Participants = log.Participants.Select(p => new CombatParticipantResponse
            {
                Id = p.Id,
                DisplayName = p.DisplayName,
                ParticipantType = p.ParticipantType,
                CurrentHP = p.CurrentHP,
                MaxHP = p.MaxHP,
                AC = p.AC,
                Initiative = p.Initiative,
                Conditions = p.Conditions.Select(c => new ConditionEntryResponse
                {
                    Name = c.Name,
                    Duration = c.Duration,
                    Description = c.Description
                }).ToList(),
                IsCurrentTurn = p.IsCurrentTurn,
                IsDead = p.IsDead
            }).ToList(),
            Events = log.Events.Select(e => new CombatLogEventResponse
            {
                Id = e.Id,
                Round = e.Round,
                TurnIndex = e.TurnIndex,
                Type = e.Type.ToString(),
                ActorName = e.ActorName,
                TargetName = e.TargetName,
                Content = e.Content,
                CreatedAt = e.CreatedAt
            }).ToList()
        };
    }

    private static CombatParticipantResponse BuildParticipantResponse(Models.CombatParticipant p)
    {
        return new CombatParticipantResponse
        {
            Id = p.Id,
            DisplayName = p.DisplayName,
            ParticipantType = p.ParticipantType,
            CurrentHP = p.CurrentHP,
            MaxHP = p.MaxHP,
            AC = p.AC,
            Initiative = p.Initiative,
            Conditions = JsonSerializer.Deserialize<List<ConditionEntryResponse>>(p.Conditions.ToString()) ?? new(),
            IsCurrentTurn = false,
            IsDead = p.CurrentHP <= 0
        };
    }

}
