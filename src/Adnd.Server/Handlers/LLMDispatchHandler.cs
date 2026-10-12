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
    ICombatTracker combats,
    ILLMInteractionLogger llmLogger,
    IGmActivityBroadcaster activity,
    ILogger<LLMDispatchHandler> logger)
{
    public async Task HandleAsync(LLMDispatchRequested msg, CancellationToken ct)
    {
        var call = await db.AgentCalls.FindAsync(msg.AgentCallId);
        if (call == null) return;

        // Every step after the first is told what the turn has done so far. Reading it here,
        // rather than carrying it on the message, is what lets a retry (AgentCallFailedHandler
        // re-publishes the original prompts) resume the turn instead of running its tools again.
        var coordinator = await db.ToolCallCoordinators.AsNoTracking()
            .FirstOrDefaultAsync(c => c.AgentCallId == msg.AgentCallId, ct);
        var turnSoFar = TurnLog.Read(coordinator?.ToolResults);
        var continuing = turnSoFar.Count > 0;
        var offerTools = !continuing
            || (turnSoFar[^1].Kind != TurnLog.Close && turnSoFar.Max(e => e.Round) < AgentSaga.MaxRounds - 1);

        var userPrompt = msg.UserPrompt;
        if (continuing)
        {
            // The system prompt was built when the player spoke; the fight has moved since.
            var combatState = call.Action == AgentAction.Narrate
                ? await combats.DescribeForNarratorAsync(msg.GameId, null, ct)
                  ?? (msg.SystemPrompt.Contains("=== COMBAT IN PROGRESS") || turnSoFar.Any(e => e.Name == "startCombat")
                      ? "No combat is in progress any more."
                      : null)
                : null;
            userPrompt = TurnLog.Continue(msg.UserPrompt, turnSoFar, combatState, offerTools);
        }

        var step = continuing ? SagaStep.LLMFollowUp : SagaStep.LLMDispatch;
        call.AdvanceStep(step);
        await db.SaveChangesAsync();
        await activity.BroadcastAsync(msg.GameId, step, ct: ct);

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
            logId = await llmLogger.LogStartAsync(game.CreatorId, msg.GameId, msg.SystemPrompt, userPrompt,
                preset.Name, preset.EndpointUrl ?? provider.EndpointUrl, preset.BaseModel);
        }
        catch (Exception logEx)
        {
            logger.LogWarning(logEx, "Failed to write LLM interaction start log for game {GameId}", msg.GameId);
        }

        var sw = Stopwatch.StartNew();
        try
        {
            if (call.Action == AgentAction.Suggest || !offerTools)
            {
                // Also the last step of a turn that has used up its rounds: without tools on
                // offer the model can only narrate, so the turn is guaranteed to end.
                // TriggerSuggest is a plain OOC Q&A — it never wants to call a tool, and its
                // own system prompt says so. But every dispatch was still offering the full
                // 12-tool schema anyway, and at least one local model (LM Studio/gemma) reacted
                // to that by "reasoning" about the question at length and then emitting no
                // content at all, over and over, even across retries. Dropping the tool
                // definitions for this action removes whatever about the tool-calling
                // apparatus was derailing it.
                var plain = await provider.CompleteAsync(msg.SystemPrompt, userPrompt, opts, ct);
                sw.Stop();
                responseText = plain.Text ?? "";
                reasoning = plain.Reasoning;
            }
            else
            {
                // The GM tool registry must be offered to the model — passing an empty list here
                // meant the GM could never call a tool, and pushed Ollama into its JSON-mode fallback.
                // After a reply that only described the attempt, narrate is taken off the
                // table. Asked to go on, a model that narrates through the tool narrated the
                // same swing again — observed live, twice running — and the turn ended with the
                // attack never rolled. It can still end the turn in plain words.
                var tools = TurnLog.LastRoundOnlySpoke(turnSoFar)
                    ? toolRegistry.GetToolDefinitions().Where(t => t.Name != "narrate")
                    : toolRegistry.GetToolDefinitions();
                var result = await provider.CompleteWithToolsAsync(
                    msg.SystemPrompt, userPrompt, tools, opts, ct);
                sw.Stop();
                responseText = result.NarrativeText ?? "";
                reasoning = result.Reasoning;
                toolCount = result.ToolCalls.Count;
                hasToolCalls = toolCount > 0;
                rawJson = hasToolCalls ? JsonSerializer.Serialize(result.ToolCalls) : null;
            }
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
