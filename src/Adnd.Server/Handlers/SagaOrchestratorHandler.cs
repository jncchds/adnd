using System.Text.Json;
using Adnd.Server.Data;
using Adnd.Server.Dtos;
using Adnd.Server.Events;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Microsoft.Extensions.Logging;

namespace Adnd.Server.Handlers;

public class SagaOrchestratorHandler(AppDbContext db, IEventBus eventBus, ILogger<SagaOrchestratorHandler> logger)
{
    public async Task HandleAsync(AgentCallQueued msg)
    {
        var call = await db.AgentCalls.FindAsync(msg.AgentCallId);
        if (call == null) return;
        call.Status = AgentCallStatus.Running;
        call.CurrentStep = (int)SagaStep.Init;
        await db.SaveChangesAsync();

        string systemPrompt, userPrompt;
        try
        {
            var opts = string.IsNullOrEmpty(call.Input)
                ? null
                : JsonSerializer.Deserialize<GMDispatchOptions>(call.Input);
            systemPrompt = opts?.SystemPrompt ?? "";
            userPrompt = opts?.UserPrompt ?? "";
        }
        catch (JsonException ex)
        {
            // Previously swallowed: a malformed Input dispatched a billable LLM call with
            // two empty prompts and no indication anything had gone wrong.
            logger.LogError(ex, "Agent call {AgentCallId} has malformed Input; aborting dispatch", call.Id);
            await eventBus.PublishAsync(new AgentCallFailed(call.Id, call.GameId, "Agent call input was not valid JSON."));
            return;
        }

        if (string.IsNullOrWhiteSpace(systemPrompt) && string.IsNullOrWhiteSpace(userPrompt))
        {
            logger.LogError("Agent call {AgentCallId} produced empty prompts; aborting dispatch", call.Id);
            await eventBus.PublishAsync(new AgentCallFailed(call.Id, call.GameId, "Agent call produced an empty prompt."));
            return;
        }

        await eventBus.PublishAsync(new LLMDispatchRequested(call.Id, call.GameId, systemPrompt, userPrompt));
    }
}
