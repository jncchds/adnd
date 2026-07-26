using System.Text.Json;
using Adnd.Server.Data;
using Adnd.Server.Events;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Adnd.Server.Services.Llm;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Handlers;

public class LLMResponseHandler(AppDbContext db, IEventBus eventBus)
{
    public async Task HandleAsync(LLMResponseReceived msg)
    {
        var call = await db.AgentCalls.FindAsync(msg.AgentCallId);
        if (call == null) return;

        call.Output = msg.ResponseText;

        if (!msg.HasToolCalls || string.IsNullOrEmpty(msg.RawJson))
        {
            call.CurrentStep = (int)SagaStep.NarrativeReady;
            var session = await db.GameSessions.FirstOrDefaultAsync(s => s.GameId == msg.GameId && s.Status == GameSessionStatus.Active);
            await db.SaveChangesAsync();
            if (session != null)
                await eventBus.PublishAsync(new NarrativeReady(msg.AgentCallId, msg.GameId, session.Id, msg.ResponseText));
            else
                await eventBus.PublishAsync(new AgentCallFailed(msg.AgentCallId, msg.GameId, "No active session found."));
            return;
        }

        call.CurrentStep = (int)SagaStep.ToolExecution;

        var toolCalls = JsonSerializer.Deserialize<List<ToolCall>>(msg.RawJson!) ?? [];
        if (toolCalls.Count == 0)
        {
            var session = await db.GameSessions.FirstOrDefaultAsync(s => s.GameId == msg.GameId && s.Status == GameSessionStatus.Active);
            await db.SaveChangesAsync();
            await eventBus.PublishAsync(new NarrativeReady(msg.AgentCallId, msg.GameId,
                session?.Id ?? Guid.Empty,
                msg.ResponseText));
            return;
        }

        var coordinator = new ToolCallCoordinator
        {
            AgentCallId = msg.AgentCallId,
            TotalTools = toolCalls.Count,
            CompletedTools = 0,
            CurrentToolIndex = 0,
            ToolResults = JsonSerializer.SerializeToElement(new List<object>())
        };
        db.ToolCallCoordinators.Add(coordinator);
        await db.SaveChangesAsync();

        var first = toolCalls[0];
        await eventBus.PublishAsync(new ToolCallRequested(msg.AgentCallId, msg.GameId, first.Name, first.Arguments.GetRawText(), 0));
    }
}
