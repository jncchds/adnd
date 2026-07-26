using Adnd.Server.Data;
using Adnd.Server.Events;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Adnd.Server.Services.Llm;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Handlers;

public class LLMFollowUpHandler(AppDbContext db, ILLMProviderFactory providerFactory, IApiKeyEncryptionService encryptionService, IEventBus eventBus)
{
    public async Task HandleAsync(LLMFollowUpRequested msg)
    {
        var call = await db.AgentCalls.FindAsync(msg.AgentCallId);
        if (call == null) return;

        call.CurrentStep = (int)SagaStep.LLMFollowUp;
        await db.SaveChangesAsync();

        var game = await db.Games.Include(g => g.LLMPreset).FirstOrDefaultAsync(g => g.Id == msg.GameId);
        if (game?.LLMPreset == null)
        {
            await eventBus.PublishAsync(new AgentCallFailed(msg.AgentCallId, msg.GameId, "No LLM preset."));
            return;
        }

        var preset = game.LLMPreset;
        if (!string.IsNullOrEmpty(preset.ApiKey))
            preset.DecryptedApiKey = encryptionService.Decrypt(preset.ApiKey);

        var provider = providerFactory.CreateFromPreset(preset);
        var opts = new LLMOptions { Model = preset.BaseModel, Temperature = preset.Temperature, MaxTokens = preset.MaxTokens };

        var followUpPrompt = $"Tool results:\n{msg.ToolResultsSummary}\n\nPlease provide your narrative response.";

        string responseText;
        try
        {
            responseText = await provider.CompleteAsync(msg.SystemPrompt, followUpPrompt, opts, CancellationToken.None);
        }
        catch (Exception ex)
        {
            await eventBus.PublishAsync(new AgentCallFailed(msg.AgentCallId, msg.GameId, ex.Message));
            return;
        }

        call.CurrentStep = (int)SagaStep.NarrativeReady;
        call.OutputMessage = responseText;
        await db.SaveChangesAsync();

        var session = await db.GameSessions.FirstOrDefaultAsync(s => s.GameId == msg.GameId && s.Status == GameSessionStatus.Active);
        await eventBus.PublishAsync(new NarrativeReady(msg.AgentCallId, msg.GameId, session?.Id ?? Guid.Empty, responseText));
    }
}
