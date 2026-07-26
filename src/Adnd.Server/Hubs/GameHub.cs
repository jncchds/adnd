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
    IDiceEngine diceEngine) : Hub
{
    protected static readonly ConcurrentDictionary<string, string> _playerConnections = new();

    protected Guid CurrentUserId =>
        Guid.Parse(Context.User!.FindFirstValue(ClaimTypes.NameIdentifier)!);

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
