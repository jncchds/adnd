using Adnd.Server.Data;
using Adnd.Server.Models;
using Adnd.Server.Services.Llm;
using Microsoft.EntityFrameworkCore;

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
    IApiKeyEncryptionService encryption) : INarrativeGenerationFactory
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
        return await provider.CompleteAsync(systemPrompt, userPrompt, opts, ct);
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
        return await provider.CompleteAsync(systemPrompt, prompt, opts, ct);
    }

    private async Task<string> BuildSystemPromptAsync(Guid gameId, Game game, CancellationToken ct)
    {
        var template = await db.PromptTemplates
            .Where(t => t.GameId == gameId || (t.GameId == null && t.IsDefault && t.Type == game.SystemId))
            .OrderByDescending(t => t.GameId.HasValue)
            .FirstOrDefaultAsync(ct);

        if (template is not null)
            return template.Content;

        return game.SystemId switch
        {
            "dnd5e" => "You are a skilled Dungeon Master running a D&D 5th Edition campaign. Your narration is vivid and immersive, your rulings fair and exciting. You balance tactical challenge with dramatic storytelling.",
            "pf2e" => "You are a skilled Game Master running a Pathfinder 2e campaign. You embrace the action economy and tactical depth of the system. Your narration is gritty and heroic.",
            "coc7e" => "You are a skilled Keeper running a Call of Cthulhu 7th Edition campaign. Your atmosphere is dread-filled and your narration relentless. The cosmos is indifferent and the horrors are real.",
            _ => "You are a skilled Game Master running a tabletop RPG. Your narration is vivid and immersive, your rulings fair and exciting."
        };
    }
}
