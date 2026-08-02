using Adnd.Server.Data;
using Adnd.Server.Models;
using Adnd.Server.Services.Llm;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Adnd.Server.Services;

public interface ILLMInteractionLogger
{
    /// <summary>Writes a Pending row before the provider call goes out. Returns its id.</summary>
    Task<Guid> LogStartAsync(Guid userId, Guid? gameId, string systemPrompt, string userPrompt,
                              string presetName, string endpointUrl, string model);

    Task LogSuccessAsync(Guid logId, string response, TokenUsage usage, long durationMs, string? reasoning = null);

    Task LogFailureAsync(Guid logId, string errorMessage, long durationMs);
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

    public async Task<Guid> LogStartAsync(Guid userId, Guid? gameId, string systemPrompt, string userPrompt,
                                           string presetName, string endpointUrl, string model)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var entry = new LLMInteractionLog
        {
            UserId = userId,
            OriginGameId = gameId,
            SystemPrompt = systemPrompt,
            UserPrompt = userPrompt,
            PresetName = presetName,
            EndpointUrl = endpointUrl,
            Model = model,
            Status = EventRecordStatus.Processing
        };
        db.LLMInteractionLogs.Add(entry);
        await db.SaveChangesAsync();

        return entry.Id;
    }

    public async Task LogSuccessAsync(Guid logId, string response, TokenUsage usage, long durationMs, string? reasoning = null)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var entry = await db.LLMInteractionLogs.FindAsync(logId);
        if (entry == null)
        {
            logger.LogWarning("LLM interaction log {LogId} not found when recording success", logId);
            return;
        }

        entry.Response = response;
        entry.Reasoning = reasoning;
        entry.PromptTokens = usage.PromptTokens;
        entry.CompletionTokens = usage.CompletionTokens;
        entry.TotalTokens = usage.TotalTokens;
        entry.DurationMs = durationMs;
        entry.Status = EventRecordStatus.Completed;
        entry.CompletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        if (DebugEnabled)
        {
            logger.LogInformation(
                "[LLM RAW] preset={Preset} model={Model} duration={Duration}ms tokens={Total}({Prompt}+{Completion})\n" +
                "=== SYSTEM ===\n{System}\n" +
                "=== USER ===\n{User}\n" +
                "=== RESPONSE ===\n{Response}",
                entry.PresetName, entry.Model, durationMs, usage.TotalTokens, usage.PromptTokens, usage.CompletionTokens,
                entry.SystemPrompt, entry.UserPrompt, response);
        }
    }

    public async Task LogFailureAsync(Guid logId, string errorMessage, long durationMs)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var entry = await db.LLMInteractionLogs.FindAsync(logId);
        if (entry == null)
        {
            logger.LogWarning("LLM interaction log {LogId} not found when recording failure", logId);
            return;
        }

        entry.ErrorMessage = errorMessage;
        entry.DurationMs = durationMs;
        entry.Status = EventRecordStatus.Failed;
        entry.CompletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
    }
}
