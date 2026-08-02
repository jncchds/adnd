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
    ILogger<LLMDispatchHandler> logger)
{
    public async Task HandleAsync(LLMDispatchRequested msg, CancellationToken ct)
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
        int toolCount = 0;
        string? rawJson = null;
        string logResponse = "";
        Exception? llmError = null;

        var sw = Stopwatch.StartNew();
        try
        {
            // The GM tool registry must be offered to the model — passing an empty list here
            // meant the GM could never call a tool, and pushed Ollama into its JSON-mode fallback.
            var result = await provider.CompleteWithToolsAsync(
                msg.SystemPrompt, msg.UserPrompt, toolRegistry.GetToolDefinitions(), opts, ct);
            sw.Stop();
            responseText = result.NarrativeText ?? "";
            toolCount = result.ToolCalls.Count;
            hasToolCalls = toolCount > 0;
            rawJson = hasToolCalls ? JsonSerializer.Serialize(result.ToolCalls) : null;
            logResponse = responseText + (rawJson ?? "");
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
                await llmLogger.LogAsync(game.CreatorId, msg.GameId, msg.SystemPrompt, msg.UserPrompt,
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

        call.CurrentStep = (int)SagaStep.LLMResponse;
        await db.SaveChangesAsync();

        await eventBus.PublishAsync(new LLMResponseReceived(msg.AgentCallId, msg.GameId, responseText, hasToolCalls, toolCount, rawJson));
    }
}
