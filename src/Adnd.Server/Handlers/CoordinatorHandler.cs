using System.Text.Json;
using Adnd.Server.Data;
using Adnd.Server.Events;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Adnd.Server.Services.Llm;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Handlers;

public class CoordinatorHandler(AppDbContext db, IEventBus eventBus)
{
    public async Task HandleAsync(ToolCallCompleted msg)
    {
        var coordinator = await db.ToolCallCoordinators.FirstOrDefaultAsync(c => c.AgentCallId == msg.AgentCallId);
        if (coordinator == null) return;

        var results = JsonSerializer.Deserialize<List<JsonElement>>(coordinator.ToolResults.GetRawText()) ?? [];
        results.Add(JsonSerializer.SerializeToElement(new { toolName = msg.ToolName, result = msg.ResultJson }));
        coordinator.ToolResults = JsonSerializer.SerializeToElement(results);
        coordinator.CompletedTools++;
        coordinator.CurrentToolIndex++;
        await db.SaveChangesAsync();

        var call = await db.AgentCalls.FindAsync(msg.AgentCallId);
        if (call == null) return;

        var rawJson = call.Output;
        if (!string.IsNullOrEmpty(rawJson))
        {
            List<ToolCall> toolCalls = [];
            try { toolCalls = JsonSerializer.Deserialize<List<ToolCall>>(rawJson) ?? []; } catch { }

            if (coordinator.CurrentToolIndex < toolCalls.Count)
            {
                var next = toolCalls[coordinator.CurrentToolIndex];
                await eventBus.PublishAsync(new ToolCallRequested(msg.AgentCallId, msg.GameId, next.Name, next.Arguments.GetRawText(), coordinator.CurrentToolIndex));
                return;
            }
        }

        call.CurrentStep = (int)SagaStep.LLMFollowUp;
        await db.SaveChangesAsync();

        var toolSummary = string.Join("\n", results.Select(r => r.GetRawText()));
        await eventBus.PublishAsync(new LLMFollowUpRequested(msg.AgentCallId, msg.GameId, "", "", toolSummary));
    }
}
