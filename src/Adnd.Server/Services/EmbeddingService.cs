using Adnd.Server.Data;
using Adnd.Server.Services.Llm;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Adnd.Server.Services;

public interface IEmbeddingService
{
    Task<float[]> GetEmbeddingAsync(string text, Guid gameId, CancellationToken ct = default);
}

public class EmbeddingService(
    AppDbContext db,
    IApiKeyEncryptionService encryption,
    ILLMProviderFactory factory,
    ILogger<EmbeddingService> logger) : IEmbeddingService
{
    public async Task<float[]> GetEmbeddingAsync(string text, Guid gameId, CancellationToken ct = default)
    {
        var game = await db.Games
            .Include(g => g.LLMPreset)
            .FirstOrDefaultAsync(g => g.Id == gameId, ct);

        if (game?.LLMPreset is null || string.IsNullOrEmpty(game.LLMPreset.EmbeddingModel))
            return [];

        var preset = game.LLMPreset;
        if (preset.ApiKey is not null)
            preset.DecryptedApiKey = encryption.Decrypt(preset.ApiKey);

        try
        {
            var provider = factory.CreateFromPreset(preset);
            return await provider.GetEmbeddingAsync(text, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Embedding failed for game {GameId}; RAG will proceed without vector", gameId);
            return [];
        }
    }
}
