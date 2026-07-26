using System.Text;
using System.Text.Json;
using Adnd.Server.Data;
using Adnd.Server.Models;
using Adnd.Server.Services.Llm;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pgvector;

namespace Adnd.Server.Services;

public interface IPlotWeaver
{
    Task ReviewAndAdaptAsync(Guid gameId, CancellationToken ct = default);
    Task<bool> HasInitialThreadsAsync(Guid gameId, CancellationToken ct = default);
}

public class PlotWeaver(
    AppDbContext db,
    IRAGService rag,
    ILLMProviderFactory factory,
    IApiKeyEncryptionService encryption,
    IEmbeddingService embeddingService,
    ILogger<PlotWeaver> logger) : IPlotWeaver
{
    public async Task ReviewAndAdaptAsync(Guid gameId, CancellationToken ct = default)
    {
        var game = await db.Games
            .Include(g => g.LLMPreset)
            .FirstOrDefaultAsync(g => g.Id == gameId, ct);

        if (game?.LLMPreset is null)
        {
            logger.LogDebug("Skipping PlotWeaver review for game {GameId}: no LLM preset", gameId);
            return;
        }

        var preset = game.LLMPreset;
        if (preset.ApiKey is not null)
            preset.DecryptedApiKey = encryption.Decrypt(preset.ApiKey);

        var provider = factory.CreateFromPreset(preset);
        var context = await rag.GeneratePlotContextAsync(gameId, ct);

        await ArchiveOldThreadsAsync(gameId, ct);

        if (!await HasInitialThreadsAsync(gameId, ct))
        {
            await GenerateInitialThreadsAsync(gameId, game.PlotSeed, provider, preset, ct);
        }
        else
        {
            await AdaptExistingThreadsAsync(gameId, context, provider, preset, ct);
            await SpawnMilestonesAsync(gameId, context, provider, preset, ct);
        }

        await DetectOpportunitiesAsync(gameId, context, provider, preset, ct);
    }

    public async Task<bool> HasInitialThreadsAsync(Guid gameId, CancellationToken ct = default)
        => await db.PlotThreads.AnyAsync(t => t.GameId == gameId && t.Status == PlotThreadStatus.Active, ct);

    // ── PlotThreadGenerationStrategy ─────────────────────────────────────────

    private async Task GenerateInitialThreadsAsync(Guid gameId, string? plotSeed, ILLMProvider provider, LLMPreset preset, CancellationToken ct)
    {
        var prompt = new StringBuilder();
        if (!string.IsNullOrEmpty(plotSeed))
            prompt.AppendLine($"Plot seed: {plotSeed}");
        prompt.AppendLine("Generate 2-4 compelling TTRPG plot threads for this game. Each should be narratively interesting and interconnected where possible.");
        prompt.AppendLine("Respond with a JSON array: [{\"title\": \"...\", \"description\": \"...\", \"category\": \"General|Faction|Mystery|Personal|Threat|WorldEvent|Relationship\", \"nextMilestone\": \"...\", \"foreshadowing\": \"...\"}]");

        var opts = new LLMOptions { Model = preset.BaseModel, Temperature = 0.9f, MaxTokens = 1024 };
        var response = await provider.CompleteAsync("You are a creative TTRPG game master. Generate compelling plot threads.", prompt.ToString(), opts, ct);

        if (!JsonExtract.TryExtractArray(response, out var arr))
        {
            logger.LogWarning("PlotWeaver: failed to extract JSON array from thread generation response for game {GameId}", gameId);
            return;
        }

        var added = 0;
        foreach (var item in arr.EnumerateArray())
        {
            if (added >= 4) break;
            var title = item.TryGetProperty("title", out var t) ? t.GetString() : null;
            if (string.IsNullOrEmpty(title)) continue;

            if (await HasSimilarThreadAsync(gameId, title, ct)) continue;

            var description = item.TryGetProperty("description", out var d) ? d.GetString() : null;
            var categoryStr = item.TryGetProperty("category", out var c) ? c.GetString() : "General";
            var nextMilestone = item.TryGetProperty("nextMilestone", out var nm) ? nm.GetString() : null;
            var foreshadowing = item.TryGetProperty("foreshadowing", out var f) ? f.GetString() : null;

            if (!Enum.TryParse<PlotThreadCategory>(categoryStr, true, out var category))
                category = PlotThreadCategory.General;

            var thread = new PlotThread
            {
                GameId = gameId,
                Title = title,
                Description = description,
                Category = category,
                NextMilestone = nextMilestone,
                Foreshadowing = foreshadowing,
                Momentum = 0f,
                IsDynamic = true
            };

            db.PlotThreads.Add(thread);
            added++;

            // Embed async — fire and forget after save
        }

        if (added > 0)
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation("PlotWeaver: generated {Count} initial plot threads for game {GameId}", added, gameId);

            // Generate embeddings for new threads
            var newThreads = await db.PlotThreads
                .Where(t => t.GameId == gameId && t.Embedding == null)
                .ToListAsync(ct);

            foreach (var thread in newThreads)
            {
                var text = $"{thread.Title}: {thread.Description}";
                var embedding = await embeddingService.GetEmbeddingAsync(text, gameId, ct);
                if (embedding.Length > 0)
                    thread.Embedding = new Vector(embedding);
            }

            await db.SaveChangesAsync(ct);
        }
    }

    // ── PlotThreadAdaptationStrategy ─────────────────────────────────────────

    private async Task AdaptExistingThreadsAsync(Guid gameId, string context, ILLMProvider provider, LLMPreset preset, CancellationToken ct)
    {
        var threads = await db.PlotThreads
            .Where(t => t.GameId == gameId && t.Status == PlotThreadStatus.Active)
            .Take(5)
            .ToListAsync(ct);

        if (threads.Count == 0) return;

        var threadList = string.Join("\n", threads.Select(t => $"- ID:{t.Id} Title:{t.Title} Momentum:{t.Momentum:F1}"));
        var prompt = $"{context}\n\nActive threads:\n{threadList}\n\nFor each thread, assess how recent events affect it. Respond with JSON array: [{{\"id\": \"guid\", \"momentum\": float(-10 to 10), \"adaptationNote\": \"...\", \"newMilestone\": \"...\"}}]";

        var opts = new LLMOptions { Model = preset.BaseModel, Temperature = 0.7f, MaxTokens = 1024 };
        var response = await provider.CompleteAsync("You are a TTRPG narrative AI. Adapt plot threads based on recent events.", prompt, opts, ct);

        if (!JsonExtract.TryExtractArray(response, out var arr)) return;

        foreach (var item in arr.EnumerateArray())
        {
            if (!item.TryGetProperty("id", out var idProp) ||
                !Guid.TryParse(idProp.GetString(), out var threadId))
                continue;

            var thread = threads.FirstOrDefault(t => t.Id == threadId);
            if (thread is null) continue;

            if (item.TryGetProperty("momentum", out var m))
                thread.Momentum = Math.Clamp(m.GetSingle(), -10f, 10f);

            if (item.TryGetProperty("adaptationNote", out var note) && !string.IsNullOrEmpty(note.GetString()))
                thread.AdaptationHistory.Add($"{DateTimeOffset.UtcNow:yyyy-MM-dd}: {note.GetString()}");

            if (item.TryGetProperty("newMilestone", out var ms) && !string.IsNullOrEmpty(ms.GetString()))
                thread.NextMilestone = ms.GetString();
        }

        await db.SaveChangesAsync(ct);
    }

    // ── PlotMilestoneSpawningStrategy ────────────────────────────────────────

    private async Task SpawnMilestonesAsync(Guid gameId, string context, ILLMProvider provider, LLMPreset preset, CancellationToken ct)
    {
        var highMomentumThreads = await db.PlotThreads
            .Where(t => t.GameId == gameId && t.Status == PlotThreadStatus.Active && t.Momentum >= 5f)
            .ToListAsync(ct);

        if (highMomentumThreads.Count == 0) return;

        foreach (var thread in highMomentumThreads)
        {
            var prompt = $"Plot thread: {thread.Title}\nDescription: {thread.Description}\nCurrent momentum: {thread.Momentum}\n\nSpawn a compelling milestone event for this thread. Respond with JSON: {{\"title\": \"...\", \"description\": \"...\"}}";

            var opts = new LLMOptions { Model = preset.BaseModel, Temperature = 0.8f, MaxTokens = 256 };
            var response = await provider.CompleteAsync("You are a TTRPG game master spawning plot milestones.", prompt, opts, ct);

            if (!JsonExtract.TryExtractObject(response, out var obj)) continue;

            var title = obj.TryGetProperty("title", out var t) ? t.GetString() : null;
            var description = obj.TryGetProperty("description", out var d) ? d.GetString() : null;

            if (string.IsNullOrEmpty(title)) continue;

            thread.MilestoneEvents.Add(new MilestoneEvent
            {
                Title = title,
                Description = description,
                Status = "Pending"
            });
        }

        await db.SaveChangesAsync(ct);
    }

    // ── PlotOpportunityDetectionStrategy ─────────────────────────────────────

    private async Task DetectOpportunitiesAsync(Guid gameId, string context, ILLMProvider provider, LLMPreset preset, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(context)) return;

        var prompt = $"{context}\n\nIdentify 1-2 story opportunities that could create new plot threads. Respond with JSON array: [{{\"title\": \"...\", \"description\": \"...\", \"category\": \"General|Faction|Mystery|Personal|Threat|WorldEvent|Relationship\"}}]";

        var opts = new LLMOptions { Model = preset.BaseModel, Temperature = 0.85f, MaxTokens = 512 };
        var response = await provider.CompleteAsync("You are a TTRPG game master identifying story opportunities.", prompt, opts, ct);

        if (!JsonExtract.TryExtractArray(response, out var arr)) return;

        var added = 0;
        foreach (var item in arr.EnumerateArray())
        {
            if (added >= 2) break;
            var title = item.TryGetProperty("title", out var t) ? t.GetString() : null;
            if (string.IsNullOrEmpty(title)) continue;

            if (await HasSimilarThreadAsync(gameId, title, ct)) continue;

            var description = item.TryGetProperty("description", out var d) ? d.GetString() : null;
            var categoryStr = item.TryGetProperty("category", out var c) ? c.GetString() : "General";

            if (!Enum.TryParse<PlotThreadCategory>(categoryStr, true, out var category))
                category = PlotThreadCategory.General;

            db.PlotThreads.Add(new PlotThread
            {
                GameId = gameId,
                Title = title,
                Description = description,
                Category = category,
                Momentum = 1f,
                IsDynamic = true
            });
            added++;
        }

        if (added > 0)
            await db.SaveChangesAsync(ct);
    }

    // ── Support methods ────────────────────────────────────────────────────────

    private async Task<bool> HasSimilarThreadAsync(Guid gameId, string title, CancellationToken ct)
    {
        var existing = await db.PlotThreads
            .Where(t => t.GameId == gameId && t.Status == PlotThreadStatus.Active)
            .Select(t => t.Title)
            .ToListAsync(ct);

        var newWords = new HashSet<string>(title.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries));

        foreach (var existingTitle in existing)
        {
            var existingWords = new HashSet<string>(existingTitle.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries));
            var intersection = newWords.Intersect(existingWords).Count();
            var union = newWords.Union(existingWords).Count();
            if (union > 0 && (float)intersection / union >= 0.5f)
                return true;
        }

        return false;
    }

    private async Task ArchiveOldThreadsAsync(Guid gameId, CancellationToken ct)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-30);
        var staleThreads = await db.PlotThreads
            .Where(t => t.GameId == gameId &&
                        (t.Status == PlotThreadStatus.Resolved || t.Status == PlotThreadStatus.Abandoned))
            .ToListAsync(ct);

        // PlotThread doesn't have an UpdatedAt, so we just soft-delete resolved threads older conceptually
        // In practice, we mark them deleted if they've been in resolved/abandoned state
        foreach (var thread in staleThreads)
        {
            thread.IsDeleted = true;
            thread.DeletedAt = DateTimeOffset.UtcNow;
        }

        if (staleThreads.Count > 0)
            await db.SaveChangesAsync(ct);
    }
}
