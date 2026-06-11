using Adnd.Server.Agent;
using Adnd.Server.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Adnd.Server.Handlers;

/// <summary>
/// Wakes up the GameAgent when a new call is queued — eliminates polling delay.
/// The agent's processing loop waits on a TaskCompletionSource when idle;
/// this handler resets the TCS to signal it to check for pending calls immediately.
/// </summary>
public class AgentCallQueuedHandler : INotificationHandler<AgentCallQueued>
{
    private readonly Services.IGameAgentManager _agentManager;
    private readonly ILogger<AgentCallQueuedHandler> _logger;

    public AgentCallQueuedHandler(Services.IGameAgentManager agentManager, ILogger<AgentCallQueuedHandler> logger)
    {
        _agentManager = agentManager;
        _logger = logger;
    }

    public Task Handle(AgentCallQueued notification, CancellationToken ct)
    {
        _logger.LogDebug("AgentCallQueued event for game {GameId}, call {CallId} — waking up agent",
            notification.GameId, notification.CallId);
        _agentManager.OnAgentCallQueued(notification.GameId, notification.CallId);
        return Task.CompletedTask;
    }
}
