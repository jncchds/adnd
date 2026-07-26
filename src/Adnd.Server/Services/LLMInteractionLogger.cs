using Adnd.Server.Data;
using Adnd.Server.Models;
using Adnd.Server.Services.Llm;

namespace Adnd.Server.Services;

public interface ILLMInteractionLogger
{
    Task LogAsync(Guid userId, Guid? gameId, string systemPrompt, string userPrompt,
                  string response, TokenUsage usage, long durationMs,
                  string presetName, string endpointUrl, string model);
}

public class LLMInteractionLogger(AppDbContext db) : ILLMInteractionLogger
{
    public async Task LogAsync(Guid userId, Guid? gameId, string systemPrompt, string userPrompt,
                               string response, TokenUsage usage, long durationMs,
                               string presetName, string endpointUrl, string model)
    {
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
    }
}
