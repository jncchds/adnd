using System.Text.Json;
using Adnd.Server.Data;
using Adnd.Server.Dtos;
using Adnd.Server.Events;
using Adnd.Server.Models;
using Adnd.Server.Services;

namespace Adnd.Server.Handlers;

public class SagaOrchestratorHandler(AppDbContext db, IEventBus eventBus)
{
    public async Task HandleAsync(AgentCallQueued msg)
    {
        var call = await db.AgentCalls.FindAsync(msg.AgentCallId);
        if (call == null) return;
        call.Status = AgentCallStatus.Running;
        call.CurrentStep = (int)SagaStep.Init;
        await db.SaveChangesAsync();

        string systemPrompt = "", userPrompt = "";
        if (!string.IsNullOrEmpty(call.Input))
        {
            try
            {
                var opts = JsonSerializer.Deserialize<GMDispatchOptions>(call.Input);
                systemPrompt = opts?.SystemPrompt ?? "";
                userPrompt = opts?.UserPrompt ?? "";
            }
            catch { }
        }

        await eventBus.PublishAsync(new LLMDispatchRequested(call.Id, call.GameId, systemPrompt, userPrompt));
    }
}
