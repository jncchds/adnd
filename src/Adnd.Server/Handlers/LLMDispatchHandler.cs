using System.Diagnostics;
using System.Text.Json;
using Adnd.Server.Data;
using Adnd.Server.Events;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Adnd.Server.Services.Llm;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Adnd.Server.Handlers;

public class LLMDispatchHandler(
    AppDbContext db,
    ILLMProviderFactory providerFactory,
    IApiKeyEncryptionService encryptionService,
    IEventBus eventBus,
    IGMToolRegistry toolRegistry,
    ILLMInteractionLogger llmLogger,
    IGmActivityBroadcaster activity,
    ILogger<LLMDispatchHandler> logger)
{
    public async Task HandleAsync(LLMDispatchRequested msg, CancellationToken ct)
    {
        var call = await db.AgentCalls.FindAsync(msg.AgentCallId);
        if (call == null) return;

        call.AdvanceStep(SagaStep.LLMDispatch);
        await db.SaveChangesAsync();
        await activity.BroadcastAsync(msg.GameId, SagaStep.LLMDispatch, ct: ct);

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
        string? reasoning = null;
        bool hasToolCalls = false;
        int toolCount = 0;
        string? rawJson = null;
        Exception? llmError = null;

        Guid logId = Guid.Empty;
        try
        {
            logId = await llmLogger.LogStartAsync(game.CreatorId, msg.GameId, msg.SystemPrompt, msg.UserPrompt,
                preset.Name, preset.EndpointUrl ?? provider.EndpointUrl, preset.BaseModel);
        }
        catch (Exception logEx)
        {
            logger.LogWarning(logEx, "Failed to write LLM interaction start log for game {GameId}", msg.GameId);
        }

        var sw = Stopwatch.StartNew();
        try
        {
            // The GM tool registry must be offered to the model — passing an empty list here
            // meant the GM could never call a tool, and pushed Ollama into its JSON-mode fallback.
            var result = await provider.CompleteWithToolsAsync(
                msg.SystemPrompt, msg.UserPrompt, toolRegistry.GetToolDefinitions(), opts, ct);
            sw.Stop();
            responseText = result.NarrativeText ?? "";
            reasoning = result.Reasoning;
            toolCount = result.ToolCalls.Count;
            hasToolCalls = toolCount > 0;
            rawJson = hasToolCalls ? JsonSerializer.Serialize(result.ToolCalls) : null;
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
                        await llmLogger.LogSuccessAsync(logId, responseText + (rawJson ?? ""),
                            provider.GetTokenUsage(), sw.ElapsedMilliseconds, reasoning);
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

        call.AdvanceStep(SagaStep.LLMResponse);
        await db.SaveChangesAsync();
        await activity.BroadcastAsync(msg.GameId, SagaStep.LLMResponse, ct: ct);

        await eventBus.PublishAsync(new LLMResponseReceived(msg.AgentCallId, msg.GameId, responseText, hasToolCalls, toolCount, rawJson));
    }
}
