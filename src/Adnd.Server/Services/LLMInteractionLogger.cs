using Adnd.Server.Data;
using Adnd.Server.Models;
using Adnd.Server.Services.Llm;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Adnd.Server.Services;

public interface ILLMInteractionLogger
{
    Task LogAsync(Guid userId, Guid? gameId, string systemPrompt, string userPrompt,
                  string response, TokenUsage usage, long durationMs,
                  string presetName, string endpointUrl, string model);
}

// Singleton: uses IServiceScopeFactory so each log write gets an isolated DbContext.
// This prevents a failed log save from poisoning the caller's DbContext tracked-entity state.
public class LLMInteractionLogger(
    IServiceScopeFactory scopeFactory,
    ILogger<LLMInteractionLogger> logger,
    IConfiguration config) : ILLMInteractionLogger
{
    private bool DebugEnabled =>
        config["LLM_DEBUG_LOG"]?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;

    public async Task LogAsync(Guid userId, Guid? gameId, string systemPrompt, string userPrompt,
                               string response, TokenUsage usage, long durationMs,
                               string presetName, string endpointUrl, string model)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.LLMInteractionLogs.Add(new LLMInteractionLog
        {
            UserId = userId,
            OriginGameId = gameId,
            SystemPrompt = systemPrompt,
            UserPrompt = userPrompt,
            Response = response,
            PromptTokens = usage.PromptTokens,
            CompletionTokens = usage.CompletionTokens,
            TotalTokens = usage.TotalTokens,
            DurationMs = durationMs,
            PresetName = presetName,
            EndpointUrl = endpointUrl,
            Model = model
        });

        await db.SaveChangesAsync();

        if (DebugEnabled)
        {
            logger.LogInformation(
                "[LLM RAW] preset={Preset} model={Model} duration={Duration}ms tokens={Total}({Prompt}+{Completion})\n" +
                "=== SYSTEM ===\n{System}\n" +
                "=== USER ===\n{User}\n" +
                "=== RESPONSE ===\n{Response}",
                presetName, model, durationMs, usage.TotalTokens, usage.PromptTokens, usage.CompletionTokens,
                systemPrompt, userPrompt, response);
        }
    }
}
