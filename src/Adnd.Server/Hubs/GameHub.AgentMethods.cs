using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Adnd.Server.Events;

namespace Adnd.Server.Hubs;

public partial class GameHub
{
    // ==================== Agent Framework ====================

    /// <summary>
    /// Send an agent call from one agent to another.
    /// </summary>
    public async Task<AgentCallResponse> CallAgent(AgentType fromAgent, AgentType toAgent,
        AgentAction action, string input, Guid? sessionId = null)
    {
        var userId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var uid))
            throw new UnauthorizedAccessException();

        // Verify player is in the game
        var player = await _context.Players
            .FirstOrDefaultAsync(p => p.UserId == uid && p.Status == PlayerStatus.Active);

        if (player == null)
            throw new InvalidOperationException("Player not active in any game.");

        var call = new AgentCall
        {
            GameId = player.GameId,
            SessionId = sessionId,
            FromAgent = fromAgent,
            ToAgent = toAgent,
            Action = action,
            Input = input,
            Status = AgentCallStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        // Queue the call
        var queuedCall = await _agentBus.SendCallAsync(call);

        // Broadcast call started
        await Clients.Group(player.GameId.ToString()).SendAsync("AgentCallStarted", new
        {
            call.Id,
            call.FromAgent,
            call.ToAgent,
            call.Action,
            call.CreatedAt
        });

        // Execute the call
        var result = await _agentBus.ExecuteCallAsync(call);

        // Broadcast completion
        await Clients.Group(player.GameId.ToString()).SendAsync("AgentCallCompleted", new
        {
            call.Id,
            call.FromAgent,
            call.ToAgent,
            call.Action,
            call.Status,
            call.Output,
            call.OutputMessage,
            call.DurationMs,
            call.CompletedAt,
            call.Error
        });

        return new AgentCallResponse
        {
            Id = call.Id,
            FromAgent = call.FromAgent,
            ToAgent = call.ToAgent,
            Action = call.Action,
            Status = call.Status,
            Output = call.Output,
            OutputMessage = call.OutputMessage,
            DurationMs = call.DurationMs,
            Error = call.Error
        };
    }

    /// <summary>
    /// Get agent call history for a game.
    /// </summary>
    public async Task<List<AgentCallResponse>> GetAgentCallHistory(Guid gameId,
        AgentType? fromAgent = null, AgentAction? action = null, int limit = 50)
    {
        var userId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var uid))
            throw new UnauthorizedAccessException();

        var game = await _context.Games.FindAsync(gameId);
        if (game == null)
            throw new KeyNotFoundException("Game not found.");

        var hasAccess = game.CreatorId == uid || game.Players.Any(p => p.UserId == uid);
        if (!hasAccess)
            throw new ForbiddenException("You don't have access to this game.");

        var calls = await _agentBus.GetCallHistoryAsync(gameId, fromAgent, action, limit);

        return calls.Select(c => new AgentCallResponse
        {
            Id = c.Id,
            FromAgent = c.FromAgent,
            ToAgent = c.ToAgent,
            Action = c.Action,
            Status = c.Status,
            Output = c.Output,
            OutputMessage = c.OutputMessage,
            DurationMs = c.DurationMs,
            Error = c.Error,
            CreatedAt = c.CreatedAt,
            CompletedAt = c.CompletedAt
        }).ToList();
    }

    /// <summary>
    /// Get a specific agent call by ID.
    /// </summary>
    public async Task<AgentCallResponse> GetAgentCall(Guid callId)
    {
        var call = await _agentBus.GetCallAsync(callId);
        if (call == null)
            throw new KeyNotFoundException($"Agent call {callId} not found.");

        return new AgentCallResponse
        {
            Id = call.Id,
            FromAgent = call.FromAgent,
            ToAgent = call.ToAgent,
            Action = call.Action,
            Status = call.Status,
            Output = call.Output,
            OutputMessage = call.OutputMessage,
            DurationMs = call.DurationMs,
            Error = call.Error,
            CreatedAt = call.CreatedAt,
            CompletedAt = call.CompletedAt
        };
    }

}
