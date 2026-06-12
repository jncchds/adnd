using Adnd.Server.Agent;
using Adnd.Server.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Adnd.Server.Handlers;

/// <summary>
/// Enqueues a pending call ID into the GameAgent's queue when a new call is queued.
/// The agent's processing loop checks this queue before querying the database.
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
        _logger.LogInformation("[AGENT_WAKEUP] Queued | GameId={GameId} | CallId={CallId} — waking up agent",
            notification.GameId, notification.CallId);
        _agentManager.OnAgentCallQueued(notification.GameId, notification.CallId);
        return Task.CompletedTask;
    }
}
