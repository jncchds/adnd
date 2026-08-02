using System.Diagnostics;
using Adnd.Server.Data;
using Adnd.Server.Events;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Adnd.Server.Services.Llm;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Adnd.Server.Handlers;

public class LLMFollowUpHandler(
    AppDbContext db,
    ILLMProviderFactory providerFactory,
    IApiKeyEncryptionService encryptionService,
    IEventBus eventBus,
    ISessionManagementService sessions,
    ILLMInteractionLogger llmLogger,
    IGmActivityBroadcaster activity,
    ILogger<LLMFollowUpHandler> logger)
{
    public async Task HandleAsync(LLMFollowUpRequested msg, CancellationToken ct)
    {
        var call = await db.AgentCalls.FindAsync(msg.AgentCallId);
        if (call == null) return;

        call.AdvanceStep(SagaStep.LLMFollowUp);
        await db.SaveChangesAsync();
        await activity.BroadcastAsync(msg.GameId, SagaStep.LLMFollowUp, ct: ct);

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

        string responseText = "";
        string? reasoning = null;
        Exception? llmError = null;

        Guid logId = Guid.Empty;
        try
        {
            logId = await llmLogger.LogStartAsync(game.CreatorId, msg.GameId, msg.SystemPrompt, followUpPrompt,
                preset.Name, preset.EndpointUrl ?? provider.EndpointUrl, preset.BaseModel);
        }
        catch (Exception logEx)
        {
            logger.LogWarning(logEx, "Failed to write LLM interaction start log for game {GameId}", msg.GameId);
        }

        var sw = Stopwatch.StartNew();
        try
        {
            var result = await provider.CompleteAsync(msg.SystemPrompt, followUpPrompt, opts, ct);
            responseText = result.Text;
            reasoning = result.Reasoning;
            sw.Stop();
        }
        catch (Exception ex)
        {
            sw.Stop();
            llmError = ex;
        }
        finally
        {
            try
            {
                if (logId != Guid.Empty)
                {
                    if (llmError != null)
                        await llmLogger.LogFailureAsync(logId, llmError.Message, sw.ElapsedMilliseconds);
                    else
                        await llmLogger.LogSuccessAsync(logId, responseText, provider.GetTokenUsage(), sw.ElapsedMilliseconds, reasoning);
                }
            }
            catch (Exception logEx)
            {
                logger.LogWarning(logEx, "Failed to write LLM interaction result log for game {GameId}", msg.GameId);
            }
        }

        if (llmError != null)
        {
            await eventBus.PublishAsync(new AgentCallFailed(msg.AgentCallId, msg.GameId, llmError.Message));
            return;
        }

        call.AdvanceStep(SagaStep.NarrativeReady);
        call.OutputMessage = responseText;
        await db.SaveChangesAsync();
        await activity.BroadcastAsync(msg.GameId, SagaStep.NarrativeReady, ct: ct);

        var session = await sessions.GetOrCreateCurrentSessionAsync(msg.GameId);
        await eventBus.PublishAsync(new NarrativeReady(msg.AgentCallId, msg.GameId, session.Id, responseText));
    }
}
