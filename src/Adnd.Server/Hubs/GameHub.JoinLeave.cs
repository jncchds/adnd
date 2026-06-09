using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Adnd.Server.Models;

namespace Adnd.Server.Hubs;

public partial class GameHub
{
    // ==================== Game Join/Leave ====================

    public async Task JoinGame(Guid gameId)
    {
        // Verify user is a player in this game
        var userId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var uid))
        {
            await Clients.Caller.SendAsync("Error", new { message = "Authentication required." });
            return;
        }

        var player = await _context.Players
            .Include(p => p.Game)
            .FirstOrDefaultAsync(p => p.GameId == gameId && p.UserId == uid);

        if (player == null || player.Game == null)
        {
            await Clients.Caller.SendAsync("Error", new { message = "You are not a player in this game." });
            return;
        }

        if (player.Status != PlayerStatus.Active)
        {
            await Clients.Caller.SendAsync("Error", new { message = "Player is not active in this game." });
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, gameId.ToString());
        await Groups.AddToGroupAsync(Context.ConnectionId, $"player:{uid}");

        // Track player-to-connection mapping
        _playerConnections[uid.ToString()] = Context.ConnectionId;

        await Clients.Group(gameId.ToString()).SendAsync("PlayerJoined", new
        {
            ConnectionId = Context.ConnectionId,
            UserId = uid,
            PlayerId = player.Id,
            CharacterName = player.CharacterName,
            Role = player.Role,
            Message = $"{player.CharacterName} joined the game"
        });

        _logger.LogInformation("Player {CharacterName} ({UserId}) joined game {GameId}",
            player.CharacterName, uid, gameId);
    }

    public async Task LeaveGame(Guid gameId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, gameId.ToString());

        var userId = Context.UserIdentifier;
        if (!string.IsNullOrEmpty(userId) && Guid.TryParse(userId, out var uid))
        {
            // Remove from player connection tracking
            _playerConnections.TryRemove(uid.ToString(), out _);

            await Clients.Group(gameId.ToString()).SendAsync("PlayerLeft", new
            {
                ConnectionId = Context.ConnectionId,
                UserId = uid,
                Message = $"Player {uid} left the game"
            });
        }
    }
}
