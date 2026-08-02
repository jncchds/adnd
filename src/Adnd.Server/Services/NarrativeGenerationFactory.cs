using System.Diagnostics;
using Adnd.Server.Data;
using Adnd.Server.Models;
using Adnd.Server.Services.Llm;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Adnd.Server.Services;

public interface INarrativeGenerationFactory
{
    Task<string> GenerateOpeningNarrationAsync(Guid gameId, CancellationToken ct = default);
    Task<string> GenerateSessionRecapAsync(Guid gameId, Guid sessionId, CancellationToken ct = default);
    Task<string> GenerateNarrativeAsync(Guid gameId, string prompt, float temperature = 0.8f, CancellationToken ct = default);
}

public class NarrativeGenerationFactory(
    AppDbContext db,
    IRAGService rag,
    ILLMProviderFactory factory,
    IApiKeyEncryptionService encryption,
    ILLMInteractionLogger llmLogger,
    ILogger<NarrativeGenerationFactory> logger) : INarrativeGenerationFactory
{
    public async Task<string> GenerateOpeningNarrationAsync(Guid gameId, CancellationToken ct = default)
    {
        var game = await db.Games
            .Include(g => g.LLMPreset)
            .FirstOrDefaultAsync(g => g.Id == gameId, ct);

        if (game?.LLMPreset is null)
            return "The adventure begins...";

        var preset = game.LLMPreset;
        if (preset.ApiKey is not null)
            preset.DecryptedApiKey = encryption.Decrypt(preset.ApiKey);

        var systemPrompt = await BuildSystemPromptAsync(gameId, game, ct);
        var context = await rag.GeneratePlotContextAsync(gameId, ct);

        var userPrompt = string.IsNullOrEmpty(game.PlotSeed)
            ? "Begin the adventure with an engaging opening narration. Set the scene, establish atmosphere, and give the players a reason to act."
            : $"Begin the adventure based on this seed: {game.PlotSeed}\n\nSet the scene with an engaging opening narration.";

        if (!string.IsNullOrEmpty(context))
            userPrompt = $"{context}\n\n{userPrompt}";

        var opts = new LLMOptions
        {
            Model = preset.BaseModel,
            Temperature = preset.Temperature,
            MaxTokens = preset.MaxTokens,
            TopP = preset.TopP
        };

        var provider = factory.CreateFromPreset(preset);
        return await CompleteAndLogAsync(gameId, game.CreatorId, provider, preset, systemPrompt, userPrompt, opts, ct);
    }

    public async Task<string> GenerateSessionRecapAsync(Guid gameId, Guid sessionId, CancellationToken ct = default)
        => await rag.GenerateSessionSummaryAsync(gameId, sessionId, ct);

    public async Task<string> GenerateNarrativeAsync(Guid gameId, string prompt, float temperature = 0.8f, CancellationToken ct = default)
    {
        var game = await db.Games
            .Include(g => g.LLMPreset)
            .FirstOrDefaultAsync(g => g.Id == gameId, ct);

        if (game?.LLMPreset is null)
            return string.Empty;

        var preset = game.LLMPreset;
        if (preset.ApiKey is not null)
            preset.DecryptedApiKey = encryption.Decrypt(preset.ApiKey);

        var systemPrompt = await BuildSystemPromptAsync(gameId, game, ct);
        var opts = new LLMOptions
        {
            Model = preset.BaseModel,
            Temperature = temperature,
            MaxTokens = preset.MaxTokens,
            TopP = preset.TopP
        };

        var provider = factory.CreateFromPreset(preset);
        return await CompleteAndLogAsync(gameId, game.CreatorId, provider, preset, systemPrompt, prompt, opts, ct);
    }

    /// <summary>
    /// Wraps a provider call with two-phase LLM interaction logging (Pending → Completed/Failed).
    /// Without this, opening narration and freeform narrative calls made directly here (rather
    /// than through AgentSaga's LLMDispatchHandler) were invisible in Admin > LLM Logs.
    /// </summary>
    private async Task<string> CompleteAndLogAsync(
        Guid gameId, Guid creatorId, ILLMProvider provider, LLMPreset preset,
        string systemPrompt, string userPrompt, LLMOptions opts, CancellationToken ct)
    {
        string response = "";
        string? reasoning = null;
        Exception? llmError = null;

        Guid logId = Guid.Empty;
        try
        {
            logId = await llmLogger.LogStartAsync(creatorId, gameId, systemPrompt, userPrompt,
                preset.Name, preset.EndpointUrl ?? provider.EndpointUrl, preset.BaseModel);
        }
        catch (Exception logEx)
        {
            logger.LogWarning(logEx, "Failed to write LLM interaction start log for game {GameId}", gameId);
        }

        var sw = Stopwatch.StartNew();
        try
        {
            var result = await provider.CompleteAsync(systemPrompt, userPrompt, opts, ct);
            response = result.Text;
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
                        await llmLogger.LogSuccessAsync(logId, response, provider.GetTokenUsage(), sw.ElapsedMilliseconds, reasoning);
                }
            }
            catch (Exception logEx)
            {
                logger.LogWarning(logEx, "Failed to write LLM interaction result log for game {GameId}", gameId);
            }
        }
        if (llmError != null)
            throw llmError;
        return response;
    }

    private async Task<string> BuildSystemPromptAsync(Guid gameId, Game game, CancellationToken ct)
    {
        var template = await db.PromptTemplates
            .Where(t => t.GameId == gameId || (t.GameId == null && t.IsDefault && t.Type == game.SystemId))
            .OrderByDescending(t => t.GameId.HasValue)
            .FirstOrDefaultAsync(ct);

        var basePrompt = template?.Content ?? game.SystemId switch
        {
            "dnd5e" => "You are a skilled Dungeon Master running a D&D 5th Edition campaign. Your narration is vivid and immersive, your rulings fair and exciting. You balance tactical challenge with dramatic storytelling.",
            "pf2e" => "You are a skilled Game Master running a Pathfinder 2e campaign. You embrace the action economy and tactical depth of the system. Your narration is gritty and heroic.",
            "coc7e" => "You are a skilled Keeper running a Call of Cthulhu 7th Edition campaign. Your atmosphere is dread-filled and your narration relentless. The cosmos is indifferent and the horrors are real.",
            _ => "You are a skilled Game Master running a tabletop RPG. Your narration is vivid and immersive, your rulings fair and exciting."
        };

        return game.LanguageDirective is { } languageDirective
            ? $"{basePrompt}\n\n{languageDirective}"
            : basePrompt;
    }
}
