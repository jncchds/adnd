using System.Collections.Concurrent;
using Adnd.Server.Data;
using Adnd.Server.Events;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Microsoft.Extensions.Logging;

namespace Adnd.Server.Handlers;

public class GameLifecycleHandler(AppDbContext db)
{
    public async Task HandleAsync(GameNarrationStarted msg)
    {
        var game = await db.Games.FindAsync(msg.GameId);
        if (game?.Status == GameStatus.Starting)
        {
            game.Status = GameStatus.Active;
            await db.SaveChangesAsync();
        }
    }
}

public class ChatHandler(ILogger<ChatHandler> logger)
{
    public Task HandleAsync(MessageSent msg)
    {
        logger.LogDebug("Message sent in game {GameId} by player {PlayerId}", msg.GameId, msg.PlayerId);
        return Task.CompletedTask;
    }
}

public class PlotWeaverHandler
{
    private static readonly ConcurrentDictionary<Guid, int> _counters = new();
    private const int TriggerEvery = 10;

    public Task HandleAsync(MessageSent msg)
    {
        _counters.AddOrUpdate(msg.GameId, 1, (_, c) => c + 1);
        return Task.CompletedTask;
    }
}

public class AgentCallFailedHandler(AppDbContext db, IDeadLetterQueue dlq, ILogger<AgentCallFailedHandler> logger)
{
    public async Task HandleAsync(AgentCallFailed msg)
    {
        var call = await db.AgentCalls.FindAsync(msg.AgentCallId);
        if (call != null)
        {
            dlq.RecordFailure(msg.AgentCallId);
            if (!dlq.ShouldRetry(msg.AgentCallId))
            {
                call.Status = AgentCallStatus.Failed;
                call.Error = msg.Error;
                call.CurrentStep = (int)SagaStep.Failed;
                await db.SaveChangesAsync();
            }
            logger.LogError("AgentCall {AgentCallId} failed: {Error}", msg.AgentCallId, msg.Error);
        }
    }
}

public class ToolCallWaitingConfirmationHandler(ILogger<ToolCallWaitingConfirmationHandler> logger)
{
    public Task HandleAsync(ToolCallWaitingConfirmation msg)
    {
        logger.LogInformation("Tool call {ToolName} waiting for confirmation in game {GameId}", msg.ToolName, msg.GameId);
        return Task.CompletedTask;
    }
}
