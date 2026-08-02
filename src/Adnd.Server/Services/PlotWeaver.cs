using System.Diagnostics;
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
    ILogger<PlotWeaver> logger,
    ILLMInteractionLogger llmLogger) : IPlotWeaver
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
            await GenerateInitialThreadsAsync(gameId, game.CreatorId, game.PlotSeed, provider, preset, ct);
        }
        else
        {
            await AdaptExistingThreadsAsync(gameId, game.CreatorId, context, provider, preset, ct);
            await SpawnMilestonesAsync(gameId, game.CreatorId, context, provider, preset, ct);
        }

        await DetectOpportunitiesAsync(gameId, game.CreatorId, context, provider, preset, ct);
    }

    public async Task<bool> HasInitialThreadsAsync(Guid gameId, CancellationToken ct = default)
        => await db.PlotThreads.AnyAsync(t => t.GameId == gameId && t.Status == PlotThreadStatus.Active, ct);

    // ── PlotThreadGenerationStrategy ─────────────────────────────────────────

    private async Task GenerateInitialThreadsAsync(Guid gameId, Guid creatorId, string? plotSeed, ILLMProvider provider, LLMPreset preset, CancellationToken ct)
    {
        var prompt = new StringBuilder();
        if (!string.IsNullOrEmpty(plotSeed))
            prompt.AppendLine($"Plot seed: {plotSeed}");
        prompt.AppendLine("Generate 2-4 compelling TTRPG plot threads for this game. Each should be narratively interesting and interconnected where possible.");
        prompt.AppendLine("Respond with a JSON array: [{\"title\": \"...\", \"description\": \"...\", \"category\": \"General|Faction|Mystery|Personal|Threat|WorldEvent|Relationship\", \"nextMilestone\": \"...\", \"foreshadowing\": \"...\"}]");

        const string system = "You are a creative TTRPG game master. Generate compelling plot threads.";
        var opts = new LLMOptions { Model = preset.BaseModel, Temperature = 0.9f, MaxTokens = 2048, JsonMode = true, JsonSchema = JsonSchemas.Array };
        var response = await CompleteAndLogAsync(gameId, creatorId, provider, preset, system, prompt.ToString(), opts, ct);

        if (!JsonExtract.TryExtractArray(response, out var arr))
        {
            logger.LogWarning("PlotWeaver: failed to extract JSON array from thread generation response for game {GameId}. Raw response: {Response}", gameId, response);
            return;
        }

        var existingTitles = await GetActiveThreadTitlesAsync(gameId, ct);

        var added = 0;
        foreach (var item in arr.EnumerateArray())
        {
            if (added >= 4) break;
            var title = item.TryGetProperty("title", out var t) ? t.GetString() : null;
            if (string.IsNullOrEmpty(title)) continue;

            if (IsSimilarToAny(existingTitles, title)) continue;
            existingTitles.Add(title);

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

    private async Task AdaptExistingThreadsAsync(Guid gameId, Guid creatorId, string context, ILLMProvider provider, LLMPreset preset, CancellationToken ct)
    {
        var threads = await db.PlotThreads
            .Where(t => t.GameId == gameId && t.Status == PlotThreadStatus.Active)
            .Take(5)
            .ToListAsync(ct);

        if (threads.Count == 0) return;

        var threadList = string.Join("\n", threads.Select(t => $"- ID:{t.Id} Title:{t.Title} Momentum:{t.Momentum:F1}"));
        var prompt = $"{context}\n\nActive threads:\n{threadList}\n\nFor each thread, assess how recent events affect it. Respond with JSON array: [{{\"id\": \"guid\", \"momentum\": float(-10 to 10), \"adaptationNote\": \"...\", \"newMilestone\": \"...\"}}]";

        const string system = "You are a TTRPG narrative AI. Adapt plot threads based on recent events.";
        var opts = new LLMOptions { Model = preset.BaseModel, Temperature = 0.7f, MaxTokens = 2048, JsonMode = true, JsonSchema = JsonSchemas.Array };
        var response = await CompleteAndLogAsync(gameId, creatorId, provider, preset, system, prompt, opts, ct);

        if (!JsonExtract.TryExtractArray(response, out var arr))
        {
            logger.LogWarning("PlotWeaver: failed to extract JSON array from thread adaptation response for game {GameId}. Raw: {Response}", gameId, response);
            return;
        }

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

    private async Task SpawnMilestonesAsync(Guid gameId, Guid creatorId, string context, ILLMProvider provider, LLMPreset preset, CancellationToken ct)
    {
        var highMomentumThreads = await db.PlotThreads
            .Where(t => t.GameId == gameId && t.Status == PlotThreadStatus.Active && t.Momentum >= 5f)
            .ToListAsync(ct);

        if (highMomentumThreads.Count == 0) return;

        const string system = "You are a TTRPG game master spawning plot milestones.";
        foreach (var thread in highMomentumThreads)
        {
            var prompt = $"Plot thread: {thread.Title}\nDescription: {thread.Description}\nCurrent momentum: {thread.Momentum}\n\nSpawn a compelling milestone event for this thread. Respond with JSON: {{\"title\": \"...\", \"description\": \"...\"}}";

            var opts = new LLMOptions { Model = preset.BaseModel, Temperature = 0.8f, MaxTokens = 512, JsonMode = true, JsonSchema = JsonSchemas.Object };
            var response = await CompleteAndLogAsync(gameId, creatorId, provider, preset, system, prompt, opts, ct);

            if (!JsonExtract.TryExtractObject(response, out var obj))
            {
                logger.LogWarning("PlotWeaver: failed to extract JSON object from milestone spawn response for thread {ThreadId} in game {GameId}. Raw: {Response}", thread.Id, gameId, response);
                continue;
            }

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

    private async Task DetectOpportunitiesAsync(Guid gameId, Guid creatorId, string context, ILLMProvider provider, LLMPreset preset, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(context)) return;

        var prompt = $"{context}\n\nIdentify 1-2 story opportunities that could create new plot threads. Respond with JSON array: [{{\"title\": \"...\", \"description\": \"...\", \"category\": \"General|Faction|Mystery|Personal|Threat|WorldEvent|Relationship\"}}]";

        const string system = "You are a TTRPG game master identifying story opportunities.";
        var opts = new LLMOptions { Model = preset.BaseModel, Temperature = 0.85f, MaxTokens = 1024, JsonMode = true, JsonSchema = JsonSchemas.Array };
        var response = await CompleteAndLogAsync(gameId, creatorId, provider, preset, system, prompt, opts, ct);

        if (!JsonExtract.TryExtractArray(response, out var arr))
        {
            logger.LogWarning("PlotWeaver: failed to extract JSON array from opportunity detection response for game {GameId}. Raw: {Response}", gameId, response);
            return;
        }

        var existingTitles = await GetActiveThreadTitlesAsync(gameId, ct);

        var added = 0;
        foreach (var item in arr.EnumerateArray())
        {
            if (added >= 2) break;
            var title = item.TryGetProperty("title", out var t) ? t.GetString() : null;
            if (string.IsNullOrEmpty(title)) continue;

            if (IsSimilarToAny(existingTitles, title)) continue;
            existingTitles.Add(title);

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

    private async Task<string> CompleteAndLogAsync(
        Guid gameId, Guid creatorId, ILLMProvider provider, LLMPreset preset,
        string systemPrompt, string userPrompt, LLMOptions opts, CancellationToken ct)
    {
        string response = "";
        Exception? llmError = null;
        var sw = Stopwatch.StartNew();
        try
        {
            response = await provider.CompleteAsync(systemPrompt, userPrompt, opts, ct);
            sw.Stop();
        }
        catch (Exception ex)
        {
            sw.Stop();
            llmError = ex;
            response = $"[ERROR] {ex.Message}";
        }
        finally
        {
            try
            {
                await llmLogger.LogAsync(creatorId, gameId, systemPrompt, userPrompt,
                    response, provider.GetTokenUsage(), sw.ElapsedMilliseconds,
                    preset.Name, preset.EndpointUrl ?? provider.EndpointUrl, preset.BaseModel);
            }
            catch (Exception logEx)
            {
                logger.LogWarning(logEx, "Failed to write LLM interaction log for game {GameId}", gameId);
            }
        }
        if (llmError != null)
            throw llmError;
        return response;
    }

    /// <summary>
    /// Loads the active thread titles once. The similarity check is called per candidate
    /// thread, and re-running this query inside that loop was a straightforward N+1.
    /// </summary>
    private Task<List<string>> GetActiveThreadTitlesAsync(Guid gameId, CancellationToken ct)
        => db.PlotThreads
            .AsNoTracking()
            .Where(t => t.GameId == gameId && t.Status == PlotThreadStatus.Active)
            .Select(t => t.Title)
            .ToListAsync(ct);

    /// <summary>Jaccard similarity over title words; ≥0.5 counts as a duplicate.</summary>
    private static bool IsSimilarToAny(IEnumerable<string> existingTitles, string title)
    {
        var newWords = new HashSet<string>(title.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries));

        foreach (var existingTitle in existingTitles)
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
        var now = DateTimeOffset.UtcNow;
        var cutoff = now.AddDays(-30);

        // Backfill ResolvedAt for threads that reached a terminal state before this field
        // existed, so they start ageing from now rather than being archived immediately.
        var missingTimestamp = await db.PlotThreads
            .Where(t => t.GameId == gameId &&
                        (t.Status == PlotThreadStatus.Resolved || t.Status == PlotThreadStatus.Abandoned) &&
                        t.ResolvedAt == null)
            .ToListAsync(ct);

        foreach (var thread in missingTimestamp)
            thread.ResolvedAt = now;

        var staleThreads = await db.PlotThreads
            .Where(t => t.GameId == gameId &&
                        (t.Status == PlotThreadStatus.Resolved || t.Status == PlotThreadStatus.Abandoned) &&
                        t.ResolvedAt != null && t.ResolvedAt < cutoff)
            .ToListAsync(ct);

        foreach (var thread in staleThreads)
        {
            thread.IsDeleted = true;
            thread.DeletedAt = now;
        }

        if (staleThreads.Count > 0 || missingTimestamp.Count > 0)
            await db.SaveChangesAsync(ct);
    }
}
