using System.Text.Json;
using Adnd.Server.Data;
using Adnd.Server.Events;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Handlers;

public class ToolExecutionHandler(AppDbContext db, IGMToolRegistry toolRegistry, IEventBus eventBus)
{
    public async Task HandleAsync(ToolCallRequested msg)
    {
        var call = await db.AgentCalls.FindAsync(msg.AgentCallId);
        if (call == null) return;

        call.CurrentStep = (int)SagaStep.ToolExecution;
        await db.SaveChangesAsync();

        if (toolRegistry.RequiresConfirmation(msg.ToolName))
        {
            var args = JsonSerializer.Deserialize<JsonElement>(msg.ArgumentsJson);
            Guid? targetPlayerId = null;
            if (args.TryGetProperty("playerId", out var pid) && Guid.TryParse(pid.GetString(), out var g))
                targetPlayerId = g;
            await eventBus.PublishAsync(new ToolCallWaitingConfirmation(msg.AgentCallId, msg.GameId, msg.ToolName, msg.ArgumentsJson, targetPlayerId));
            return;
        }

        try
        {
            var session = await db.GameSessions.FirstOrDefaultAsync(s => s.GameId == msg.GameId && s.Status == GameSessionStatus.Active);
            var sessionId = session?.Id ?? Guid.Empty;
            var args = JsonSerializer.Deserialize<JsonElement>(msg.ArgumentsJson);
            var result = await toolRegistry.ExecuteToolAsync(msg.ToolName, args, msg.GameId, sessionId, CancellationToken.None);
            await eventBus.PublishAsync(new ToolCallCompleted(msg.AgentCallId, msg.GameId, msg.ToolName, result, msg.ToolIndex, 0));
        }
        catch (Exception ex)
        {
            await eventBus.PublishAsync(new ToolCallCompleted(msg.AgentCallId, msg.GameId, msg.ToolName, $"Error: {ex.Message}", msg.ToolIndex, 0));
        }
    }
}
