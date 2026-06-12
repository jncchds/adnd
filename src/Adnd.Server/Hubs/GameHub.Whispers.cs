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
