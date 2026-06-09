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
    // ==================== Whispers ====================

    /// <summary>
    /// Send a whisper from a player to specific target(s).
    /// Targets format: "player:{userId}" or "all" or "group:{groupName}"
    /// </summary>
    public async Task SendWhisper(string targets, string content)
    {
        var userId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var uid))
        {
            await Clients.Caller.SendAsync("Error", new { message = "Authentication required." });
            return;
        }

        var player = await _context.Players
            .Include(p => p.Game)
            .FirstOrDefaultAsync(p => p.UserId == uid && p.Status == PlayerStatus.Active);

        if (player == null || player.Game == null)
        {
            await Clients.Caller.SendAsync("Error", new { message = "You are not an active player." });
            return;
        }

        // Check whisper permission
        if (!_whisperService.CanWhisper(player))
        {
            await Clients.Caller.SendAsync("Error", new { message = "You do not have whisper permission." });
            return;
        }

        Adnd.Server.Models.WhisperType whisperType;
        if (targets == "all")
        {
            whisperType = player.Role == PlayerRole.Creator ? Adnd.Server.Models.WhisperType.GMToAll : Adnd.Server.Models.WhisperType.PlayerToPlayer;
        }
        else if (targets.Contains("player:", StringComparison.OrdinalIgnoreCase))
        {
            var isCreatorToPlayer = player.Role == PlayerRole.Creator;
            whisperType = isCreatorToPlayer ? Adnd.Server.Models.WhisperType.InGameGMToPlayer : Adnd.Server.Models.WhisperType.PlayerToPlayer;
        }
        else
        {
            whisperType = player.Role == PlayerRole.Creator ? Adnd.Server.Models.WhisperType.GMToGroup : Adnd.Server.Models.WhisperType.PlayerToPlayer;
        }

        var whisper = await _whisperService.SendWhisperAsync(
            player.GameId, Guid.Empty, player.Id, targets, whisperType, content);

        // Parse targets to determine who receives the whisper
        var targetIds = _whisperService.ParseTargets(targets);

        if (targets == "all" || string.IsNullOrEmpty(targets))
        {
            await Clients.Group(player.GameId.ToString()).SendAsync("NewWhisper", BuildWhisperResponse(whisper, player.CharacterName, player.Role));
        }
        else
        {
            foreach (var targetId in targetIds)
            {
                var targetPlayer = await _context.Players
                    .FirstOrDefaultAsync(p => p.Id == targetId && p.GameId == player.GameId);

                if (targetPlayer == null) continue;
                var targetConnectionId = GetConnectionIdForPlayer(targetPlayer.Id);
                if (targetConnectionId != null)
                {
                    await Clients.Client(targetConnectionId)
                        .SendAsync("NewWhisper", BuildWhisperResponse(whisper, player.CharacterName, player.Role));
                }
            }

            await Clients.Caller.SendAsync("NewWhisper", new
            {
                whisper.Id,
                FromPlayerId = whisper.FromPlayerId,
                FromCharacter = player.CharacterName,
                FromRole = player.Role,
                Content = whisper.Content,
                Type = whisper.Type,
                Targets = whisper.Targets,
                CreatedAt = whisper.CreatedAt,
                IsSent = true
            });
        }

        _logger.LogInformation("Whisper from {CharacterName} to [{Targets}]: {Content}",
            player.CharacterName, targets, content);
    }

    /// <summary>
    /// GM sends a whisper to a specific player.
    /// </summary>
    public async Task SendGMWhisper(Guid targetPlayerId, string content)
    {
        var userId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var uid))
        {
            await Clients.Caller.SendAsync("Error", new { message = "Authentication required." });
            return;
        }

        var gmPlayer = await _context.Players
            .FirstOrDefaultAsync(p => p.UserId == uid && p.Status == PlayerStatus.Active);

        if (gmPlayer == null || gmPlayer.Role != PlayerRole.Creator)
        {
            await Clients.Caller.SendAsync("Error", new { message = "Only the game creator can send creator whispers." });
            return;
        }

        var targetPlayer = await _context.Players
            .FirstOrDefaultAsync(p => p.Id == targetPlayerId && p.GameId == gmPlayer.GameId && p.Status == PlayerStatus.Active);

        if (targetPlayer == null)
        {
            await Clients.Caller.SendAsync("Error", new { message = "Target player not found or inactive." });
            return;
        }

        var whisper = await _whisperService.SendGMWhisperAsync(
            gmPlayer.GameId, Guid.Empty, gmPlayer.Id,
            new List<Guid> { targetPlayerId }, Adnd.Server.Models.WhisperType.InGameGMToPlayer, content);

        // Send to the target player
        var targetConnectionId = GetConnectionIdForPlayer(targetPlayer.Id);
        if (targetConnectionId != null)
        {
            await Clients.Client(targetConnectionId)
                .SendAsync("NewWhisper", BuildWhisperResponse(whisper, gmPlayer.CharacterName, gmPlayer.Role));
        }

        // Confirmation to GM
        await Clients.Caller.SendAsync("NewWhisper", new
        {
            whisper.Id,
            FromPlayerId = whisper.FromPlayerId,
            FromCharacter = gmPlayer.CharacterName,
            FromRole = gmPlayer.Role,
            Content = whisper.Content,
            Type = whisper.Type,
            Targets = whisper.Targets,
            CreatedAt = whisper.CreatedAt,
            IsSent = true
        });

        _logger.LogInformation("GM whisper from {GM} to {Target}: {Content}",
            gmPlayer.CharacterName, targetPlayer.CharacterName, content);
    }

    /// <summary>
    /// Get whisper history for the current player.
    /// </summary>
    public async Task<List<WhisperResponse>> GetWhisperHistory(Guid gameId, int limit = 50)
    {
        var userId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var uid))
            throw new UnauthorizedAccessException();

        var player = await _context.Players
            .FirstOrDefaultAsync(p => p.UserId == uid && p.GameId == gameId);

        if (player == null)
            throw new InvalidOperationException("Player not found in game.");

        var isCreator = player.Role == PlayerRole.Creator;
        var whispers = await _whisperService.GetWhispersForPlayerAsync(gameId, player.Id, isCreator, limit);

        return whispers.Select(w => new WhisperResponse
        {
            Id = w.Id,
            FromPlayerId = w.FromPlayerId,
            FromCharacter = w.FromPlayer?.CharacterName ?? "Unknown",
            FromRole = w.FromPlayer?.Role ?? PlayerRole.Player,
            Content = w.Content,
            Type = w.Type,
            Targets = w.Targets,
            CreatedAt = w.CreatedAt
        }).ToList();
    }

}
