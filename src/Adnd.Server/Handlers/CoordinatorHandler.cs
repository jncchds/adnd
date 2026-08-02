using System.Text.Json;
using Adnd.Server.Data;
using Adnd.Server.Dtos;
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

        // Read the pending tool calls from the coordinator. This previously read
        // AgentCall.Output — the narrative text — which always failed to deserialize into
        // a tool list, so tools 2..N were silently never executed.
        var toolCalls = coordinator.ToolCalls.ValueKind == JsonValueKind.Array
            ? JsonSerializer.Deserialize<List<ToolCall>>(coordinator.ToolCalls.GetRawText()) ?? []
            : [];

        if (coordinator.CurrentToolIndex < toolCalls.Count)
        {
            var next = toolCalls[coordinator.CurrentToolIndex];
            await eventBus.PublishAsync(new ToolCallRequested(msg.AgentCallId, msg.GameId, next.Name, next.Arguments.GetRawText(), coordinator.CurrentToolIndex));
            return;
        }

        call.CurrentStep = (int)SagaStep.LLMFollowUp;
        await db.SaveChangesAsync();

        // Carry the original prompts into the follow-up. These used to be sent as empty
        // strings, so the follow-up call lost all GM persona and world context.
        string systemPrompt = "", userPrompt = "";
        if (!string.IsNullOrEmpty(call.Input))
        {
            try
            {
                var opts = JsonSerializer.Deserialize<GMDispatchOptions>(call.Input);
                systemPrompt = opts?.SystemPrompt ?? "";
                userPrompt = opts?.UserPrompt ?? "";
            }
            catch (JsonException)
            {
                // Dispatch already validated Input; a failure here is non-fatal.
            }
        }

        var toolSummary = string.Join("\n", results.Select(r => r.GetRawText()));
        await eventBus.PublishAsync(new LLMFollowUpRequested(msg.AgentCallId, msg.GameId, systemPrompt, userPrompt, toolSummary));
    }
}
