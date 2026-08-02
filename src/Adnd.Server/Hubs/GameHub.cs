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
    IDiceEngine diceEngine,
    IRAGService rag,
    INPCRelevanceService npcRelevance,
    IGmActivityBroadcaster gmActivity) : Hub
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

    /// <summary>GM-only actions: narration, GM whispers, combat control.</summary>
    protected async Task<Player> RequireCreatorAsync(Guid gameId)
    {
        var player = await RequireMemberAsync(gameId);
        if (player.Role != PlayerRole.Creator)
            throw new HubForbiddenException("Only the Game Master can perform this action.");
        return player;
    }

    /// <summary>Confirms the combat belongs to the game before acting on it.</summary>
    protected async Task<Combat> RequireCombatInGameAsync(Guid gameId, Guid combatId)
    {
        var combat = await db.Combats.Include(c => c.Participants)
            .FirstOrDefaultAsync(c => c.Id == combatId && c.GameId == gameId);
        return combat ?? throw new HubForbiddenException("Combat does not belong to this game.");
    }

    public override async Task OnConnectedAsync()
    {
        _playerConnections[Context.ConnectionId] = CurrentUserId.ToString();
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _playerConnections.TryRemove(Context.ConnectionId, out _);
        await base.OnDisconnectedAsync(exception);
    }

    protected async Task PersistGameEventAsync(Guid sessionId, string content, string messageType, Guid? playerId = null, bool isOOC = false)
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
    }

    protected async Task<GameSession> ResolveGameSessionAsync(Guid gameId)
        => await sessionService.GetOrCreateCurrentSessionAsync(gameId);

    protected async Task PublishAsync<T>(T @event) where T : class
        => await eventBus.PublishAsync(@event);

    protected Task BroadcastToGameAsync(Guid gameId, string method, object payload)
        => Clients.Group(gameId.ToString()).SendAsync(method, payload);
}
