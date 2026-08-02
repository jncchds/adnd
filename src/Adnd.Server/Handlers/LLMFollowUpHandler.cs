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
    ILogger<LLMFollowUpHandler> logger)
{
    public async Task HandleAsync(LLMFollowUpRequested msg, CancellationToken ct)
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

        string responseText = "";
        string logResponse = "";
        Exception? llmError = null;

        var sw = Stopwatch.StartNew();
        try
        {
            responseText = await provider.CompleteAsync(msg.SystemPrompt, followUpPrompt, opts, ct);
            sw.Stop();
            logResponse = responseText;
        }
        catch (Exception ex)
        {
            sw.Stop();
            llmError = ex;
            logResponse = $"[ERROR] {ex.Message}";
        }
        finally
        {
            try
            {
                await llmLogger.LogAsync(game.CreatorId, msg.GameId, msg.SystemPrompt, followUpPrompt,
                    logResponse, provider.GetTokenUsage(), sw.ElapsedMilliseconds,
                    preset.Name, preset.EndpointUrl ?? provider.EndpointUrl, preset.BaseModel);
            }
            catch (Exception logEx)
            {
                logger.LogWarning(logEx, "Failed to write LLM interaction log for game {GameId}", msg.GameId);
            }
        }

        if (llmError != null)
        {
            await eventBus.PublishAsync(new AgentCallFailed(msg.AgentCallId, msg.GameId, llmError.Message));
            return;
        }

        call.CurrentStep = (int)SagaStep.NarrativeReady;
        call.OutputMessage = responseText;
        await db.SaveChangesAsync();

        var session = await sessions.GetOrCreateCurrentSessionAsync(msg.GameId);
        await eventBus.PublishAsync(new NarrativeReady(msg.AgentCallId, msg.GameId, session.Id, responseText));
    }
}
