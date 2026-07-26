using System.Text.Json;
using Adnd.Server.Data;
using Adnd.Server.Events;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Adnd.Server.Services.Llm;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Handlers;

public class LLMDispatchHandler(AppDbContext db, ILLMProviderFactory providerFactory, IApiKeyEncryptionService encryptionService, IEventBus eventBus)
{
    public async Task HandleAsync(LLMDispatchRequested msg)
    {
        var call = await db.AgentCalls.FindAsync(msg.AgentCallId);
        if (call == null) return;

        call.CurrentStep = (int)SagaStep.LLMDispatch;
        await db.SaveChangesAsync();

        var game = await db.Games.Include(g => g.LLMPreset).FirstOrDefaultAsync(g => g.Id == msg.GameId);
        if (game?.LLMPreset == null)
        {
            await eventBus.PublishAsync(new AgentCallFailed(msg.AgentCallId, msg.GameId, "No LLM preset configured for this game."));
            return;
        }

        var preset = game.LLMPreset;
        if (!string.IsNullOrEmpty(preset.ApiKey))
            preset.DecryptedApiKey = encryptionService.Decrypt(preset.ApiKey);

        var provider = providerFactory.CreateFromPreset(preset);
        var opts = new LLMOptions
        {
            Model = preset.BaseModel,
            Temperature = preset.Temperature,
            MaxTokens = preset.MaxTokens,
            TopP = preset.TopP,
            FrequencyPenalty = preset.FrequencyPenalty,
            PresencePenalty = preset.PresencePenalty,
            Stream = false
        };

        string responseText = "";
        bool hasToolCalls = false;
        string? rawJson = null;

        try
        {
            var result = await provider.CompleteWithToolsAsync(msg.SystemPrompt, msg.UserPrompt, [], opts, CancellationToken.None);
            responseText = result.NarrativeText ?? "";
            hasToolCalls = result.ToolCalls.Count > 0;
            rawJson = hasToolCalls ? JsonSerializer.Serialize(result.ToolCalls) : null;
        }
        catch (Exception ex)
        {
            await eventBus.PublishAsync(new AgentCallFailed(msg.AgentCallId, msg.GameId, ex.Message));
            return;
        }

        call.CurrentStep = (int)SagaStep.LLMResponse;
        await db.SaveChangesAsync();

        await eventBus.PublishAsync(new LLMResponseReceived(msg.AgentCallId, msg.GameId, responseText, hasToolCalls, 0, rawJson));
    }
}
