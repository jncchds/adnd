using System.Text.Json;
using Adnd.Server.Data;
using Adnd.Server.Events;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Adnd.Server.Handlers;

public class ToolExecutionHandler(
    AppDbContext db,
    IGMToolRegistry toolRegistry,
    IEventBus eventBus,
    ISessionManagementService sessions,
    IGmActivityBroadcaster activity,
    ILogger<ToolExecutionHandler> logger)
{
    public async Task HandleAsync(ToolCallRequested msg, CancellationToken ct)
    {
        var call = await db.AgentCalls.FindAsync([msg.AgentCallId], ct);
        if (call == null) return;

        call.AdvanceStep(SagaStep.ToolExecution);
        await db.SaveChangesAsync(ct);
        await activity.BroadcastAsync(msg.GameId, SagaStep.ToolExecution, msg.ToolName, ct);

        if (toolRegistry.RequiresConfirmation(msg.ToolName))
        {
            var pending = JsonSerializer.Deserialize<JsonElement>(msg.ArgumentsJson);
            Guid? targetPlayerId = null;
            if (pending.TryGetProperty("playerId", out var pid) && Guid.TryParse(pid.GetString(), out var g))
                targetPlayerId = g;

            var pendingSession = await sessions.GetOrCreateCurrentSessionAsync(msg.GameId);

            // Record the pending tool call so it can be surfaced via /api/gmtools/pending
            // and resolved later. Without this the saga simply stalled until the 5-minute timeout.
            db.GMToolCalls.Add(new GMToolCall
            {
                AgentCallId = msg.AgentCallId,
                GameId = msg.GameId,
                SessionId = pendingSession.Id,
                ToolName = msg.ToolName,
                Arguments = pending,
                ToolIndex = msg.ToolIndex,
                RequiresConfirmation = true,
                Status = GMToolCallStatus.AwaitingConfirmation,
                TargetPlayerId = targetPlayerId
            });
            await db.SaveChangesAsync(ct);

            await eventBus.PublishAsync(new ToolCallWaitingConfirmation(msg.AgentCallId, msg.GameId, msg.ToolName, msg.ArgumentsJson, targetPlayerId), ct);
            return;
        }

        await ExecuteAndPublishAsync(msg.AgentCallId, msg.GameId, msg.ToolName, msg.ArgumentsJson, msg.ToolIndex, ct);
    }

    /// <summary>Resumes the chain once a player confirms or declines a gated tool call.</summary>
    public async Task HandleAsync(ToolCallConfirmationResolved msg, CancellationToken ct)
    {
        if (!msg.Approved)
        {
            var reason = string.IsNullOrWhiteSpace(msg.DeclineReason) ? "Player declined." : msg.DeclineReason;
            await eventBus.PublishAsync(new ToolCallCompleted(msg.AgentCallId, msg.GameId, msg.ToolName, $"Declined: {reason}", msg.ToolIndex, 0), ct);
            return;
        }

        await ExecuteAndPublishAsync(msg.AgentCallId, msg.GameId, msg.ToolName, msg.ArgumentsJson, msg.ToolIndex, ct);
    }

    private async Task ExecuteAndPublishAsync(Guid agentCallId, Guid gameId, string toolName, string argumentsJson, int toolIndex, CancellationToken ct)
    {
        var totalTools = await db.ToolCallCoordinators
            .Where(c => c.AgentCallId == agentCallId)
            .Select(c => c.TotalTools)
            .FirstOrDefaultAsync(ct);

        try
        {
            var session = await sessions.GetOrCreateCurrentSessionAsync(gameId);
            var args = JsonSerializer.Deserialize<JsonElement>(argumentsJson);
            var result = await toolRegistry.ExecuteToolAsync(toolName, args, gameId, session.Id, ct);
            await eventBus.PublishAsync(new ToolCallCompleted(agentCallId, gameId, toolName, result, toolIndex, totalTools), ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Tool '{ToolName}' execution failed for agent call {AgentCallId}", toolName, agentCallId);
            await eventBus.PublishAsync(new ToolCallCompleted(agentCallId, gameId, toolName, $"Error: {ex.Message}", toolIndex, totalTools), ct);
        }
    }
}
