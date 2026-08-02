using System.Text.Json;
using Adnd.Server.Data;
using Adnd.Server.Events;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Adnd.Server.Services.Llm;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Handlers;

public class LLMResponseHandler(AppDbContext db, IEventBus eventBus, ISessionManagementService sessions)
{
    public async Task HandleAsync(LLMResponseReceived msg)
    {
        var call = await db.AgentCalls.FindAsync(msg.AgentCallId);
        if (call == null) return;

        call.Output = msg.ResponseText;

        List<ToolCall> toolCalls = [];
        if (msg.HasToolCalls && !string.IsNullOrEmpty(msg.RawJson))
            toolCalls = JsonSerializer.Deserialize<List<ToolCall>>(msg.RawJson) ?? [];

        if (toolCalls.Count == 0)
        {
            call.CurrentStep = (int)SagaStep.NarrativeReady;
            await db.SaveChangesAsync();

            var session = await sessions.GetOrCreateCurrentSessionAsync(msg.GameId);
            await eventBus.PublishAsync(new NarrativeReady(msg.AgentCallId, msg.GameId, session.Id, msg.ResponseText));
            return;
        }

        call.CurrentStep = (int)SagaStep.ToolExecution;

        var coordinator = new ToolCallCoordinator
        {
            AgentCallId = msg.AgentCallId,
            TotalTools = toolCalls.Count,
            CompletedTools = 0,
            CurrentToolIndex = 0,
            ToolResults = JsonSerializer.SerializeToElement(new List<object>()),
            ToolCalls = JsonSerializer.SerializeToElement(toolCalls)
        };
        db.ToolCallCoordinators.Add(coordinator);
        await db.SaveChangesAsync();

        var first = toolCalls[0];
        await eventBus.PublishAsync(new ToolCallRequested(msg.AgentCallId, msg.GameId, first.Name, first.Arguments.GetRawText(), 0));
    }
}
