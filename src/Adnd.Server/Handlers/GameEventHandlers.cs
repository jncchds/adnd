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

// Tracks per-game message counts and triggers PlotWeaver every N messages
// Also handles S9 event-based triggers
public class PlotWeaverHandler(IPlotWeaver plotWeaver, ILogger<PlotWeaverHandler> logger)
{
    private static readonly ConcurrentDictionary<Guid, int> _counters = new();
    private const int TriggerEvery = 10;

    public async Task HandleAsync(MessageSent msg)
    {
        var count = _counters.AddOrUpdate(msg.GameId, 1, (_, c) => c + 1);
        if (count % TriggerEvery == 0)
        {
            logger.LogInformation("PlotWeaver triggered by message count ({Count}) for game {GameId}", count, msg.GameId);
            await SafeReviewAsync(msg.GameId);
        }
    }

    // S9 — trigger PlotWeaver on CombatEnded
    public async Task HandleAsync(CombatEnded msg)
    {
        logger.LogInformation("PlotWeaver triggered by CombatEnded for game {GameId}", msg.GameId);
        await SafeReviewAsync(msg.GameId);
    }

    // S9 — trigger PlotWeaver when a new session is created
    public async Task HandleAsync(SessionCreated msg)
    {
        logger.LogInformation("PlotWeaver triggered by SessionCreated for game {GameId}", msg.GameId);
        await SafeReviewAsync(msg.GameId);
    }

    // S9 — trigger PlotWeaver on story sway
    public async Task HandleAsync(StorySwayed msg)
    {
        logger.LogInformation("PlotWeaver triggered by StorySwayed for game {GameId}", msg.GameId);
        await SafeReviewAsync(msg.GameId);
    }

    private async Task SafeReviewAsync(Guid gameId)
    {
        try
        {
            await plotWeaver.ReviewAndAdaptAsync(gameId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "PlotWeaver review failed for game {GameId}", gameId);
        }
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
