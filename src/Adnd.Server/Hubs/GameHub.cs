using System.Collections.Concurrent;
using System.Security.Claims;
using Adnd.Server.Data;
using Adnd.Server.Events;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Hubs;

[Authorize]
public partial class GameHub(
    AppDbContext db,
    IEventBus eventBus,
    ISessionManagementService sessionService,
    IAgentBus agentBus,
    IRAGService rag,
    INPCRelevanceService npcRelevance,
    IGmActivityBroadcaster gmActivity,
    ICombatTracker combats,
    IStoryNudgeService nudges) : Hub
{
    protected static readonly ConcurrentDictionary<string, string> _playerConnections = new();

    /// <summary>
    /// Thrown when a hub method is called against a game the caller is not part of.
    /// SignalR surfaces the message to the caller and aborts the invocation.
    /// </summary>
    protected sealed class HubForbiddenException(string message) : HubException(message);

    protected Guid CurrentUserId
    {
        get
        {
            // Both null-forgiving operators here used to be load-bearing: a token without a
            // parseable nameid faulted OnConnectedAsync instead of failing cleanly.
            var raw = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(raw, out var id)
                ? id
                : throw new HubException("Invalid or missing user identity.");
        }
    }

    /// <summary>
    /// Every hub method must call this before touching a game. Only JoinGameGroup used to
    /// verify membership, and Clients.Group() does not require the caller to be in the
    /// group — so any authenticated user could post, whisper as the GM, drive combat, or
    /// spend another user's LLM budget in a game they had nothing to do with.
    /// </summary>
    protected async Task<Player> RequireMemberAsync(Guid gameId)
    {
        var userId = CurrentUserId;
        var player = await db.Players.FirstOrDefaultAsync(p => p.GameId == gameId && p.UserId == userId);
        return player ?? throw new HubForbiddenException("You are not a member of this game.");
    }

    public override async Task OnConnectedAsync()
    {
        _playerConnections[Context.ConnectionId] = CurrentUserId.ToString();
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _playerConnections.TryRemove(Context.ConnectionId, out var userIdText);

        // The client never calls LeaveGameGroup — it just closes the socket — so IsConnected
        // used to go true on the first join and stay true forever. A user with another tab
        // still open is still at the table.
        if (userIdText is not null
            && !_playerConnections.Values.Contains(userIdText)
            && Guid.TryParse(userIdText, out var userId))
        {
            var seats = await db.Players.Where(p => p.UserId == userId && p.IsConnected).ToListAsync();
            foreach (var seat in seats) seat.IsConnected = false;
            if (seats.Count > 0) await db.SaveChangesAsync();
            foreach (var seat in seats)
                await BroadcastToGameAsync(seat.GameId, "PlayerDisconnected", new { userId });
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>Returns the saved row so callers that also broadcast it send the id that was
    /// actually persisted, rather than minting a second one the client can never match.</summary>
    protected async Task<Message> PersistGameEventAsync(Guid sessionId, string content, string messageType, Guid? playerId = null, bool isOOC = false)
    {
        var msg = new Message
        {
            SessionId = sessionId,
            PlayerId = playerId,
            Content = content,
            Type = messageType,
            IsOOC = isOOC,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Messages.Add(msg);
        await db.SaveChangesAsync();
        return msg;
    }

    protected async Task<GameSession> ResolveGameSessionAsync(Guid gameId)
        => await sessionService.GetOrCreateCurrentSessionAsync(gameId);

    protected async Task PublishAsync<T>(T @event) where T : class
        => await eventBus.PublishAsync(@event);

    protected Task BroadcastToGameAsync(Guid gameId, string method, object payload)
        => Clients.Group(gameId.ToString()).SendAsync(method, payload);
}
