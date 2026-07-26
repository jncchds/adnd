using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Adnd.Server.Data;
using Adnd.Server.Models;
using Adnd.Server.Services.Llm;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace Adnd.Server.Services;

public record ConsistencyReport(bool IsConsistent, List<string> Issues, string Summary);
public record PlotContinuation(string Suggestion, List<string> PossibleDirections);

public interface IRAGService
{
    Task<string> GeneratePlotContextAsync(Guid gameId, CancellationToken ct = default);
    Task<List<PlotThread>> FindSimilarPlotThreadsAsync(Guid gameId, float[] queryEmbedding, int topK = 5, CancellationToken ct = default);
    Task<string> GenerateSessionSummaryAsync(Guid gameId, Guid sessionId, CancellationToken ct = default);
    Task<ConsistencyReport> CheckPlotConsistencyAsync(Guid gameId, CancellationToken ct = default);
    Task<PlotContinuation> SuggestContinuationAsync(Guid gameId, CancellationToken ct = default);
    Task EmbedMessagesAsync(Guid gameId, CancellationToken ct = default);
    Task EmbedMessageAsync(Guid messageId, CancellationToken ct = default);
}

public class RAGService(
    AppDbContext db,
    IEmbeddingService embeddingService,
    ILLMProviderFactory factory,
    IApiKeyEncryptionService encryption) : IRAGService
{
    // Static cache shared across all scoped instances; 60-minute TTL
    private static readonly ConcurrentDictionary<string, (float[] Embedding, DateTimeOffset CachedAt)> _embeddingCache = new();
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(60);

    public async Task<string> GeneratePlotContextAsync(Guid gameId, CancellationToken ct = default)
    {
        var sb = new StringBuilder();

        // Recent messages (last 20 non-OOC, non-whisper)
        var session = await db.GameSessions
            .Where(s => s.GameId == gameId && s.Status == GameSessionStatus.Active)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (session is not null)
        {
            var messages = await db.Messages
                .Where(m => m.SessionId == session.Id && !m.IsOOC && m.WhisperToId == null)
                .OrderByDescending(m => m.CreatedAt)
                .Take(20)
                .OrderBy(m => m.CreatedAt)
                .Include(m => m.Player)
                .ToListAsync(ct);

            if (messages.Count > 0)
            {
                sb.AppendLine("=== RECENT MESSAGES ===");
                foreach (var msg in messages)
                {
                    var speaker = msg.Player?.CharacterName ?? "GM";
                    sb.AppendLine($"{speaker}: {msg.Content}");
                }
                sb.AppendLine();
            }
        }

        // Active NPCs
        var npcs = await db.NPCs
            .Where(n => n.GameId == gameId)
            .ToListAsync(ct);

        if (npcs.Count > 0)
        {
            sb.AppendLine("=== ACTIVE NPCs ===");
            foreach (var npc in npcs)
            {
                var tags = new List<string> { npc.Attitude.ToString() };
                if (!string.IsNullOrEmpty(npc.Faction)) tags.Add($"{npc.Faction} faction");
                var tagStr = $" [{string.Join(", ", tags)}]";
                sb.AppendLine($"{npc.Name}{tagStr}: {npc.Description}");
            }
            sb.AppendLine();
        }

        // Active plot threads
        var threads = await db.PlotThreads
            .Where(t => t.GameId == gameId && t.Status == PlotThreadStatus.Active)
            .OrderByDescending(t => Math.Abs(t.Momentum))
            .Take(10)
            .ToListAsync(ct);

        if (threads.Count > 0)
        {
            sb.AppendLine("=== ACTIVE PLOT THREADS ===");
            foreach (var thread in threads)
            {
                sb.AppendLine($"- {thread.Title} [Momentum: {thread.Momentum:+0.#;-0.#;0}]");
                if (!string.IsNullOrEmpty(thread.Description))
                    sb.AppendLine($"  {thread.Description}");
                if (!string.IsNullOrEmpty(thread.NextMilestone))
                    sb.AppendLine($"  Next: {thread.NextMilestone}");
            }
            sb.AppendLine();
        }

        // Character backgrounds (S2 — included when filled in)
        var characters = await db.Characters
            .Include(c => c.Player)
            .Where(c => c.Player.GameId == gameId)
            .ToListAsync(ct);

        var charsWithBios = characters.Where(c => !string.IsNullOrEmpty(c.Background)).ToList();
        if (charsWithBios.Count > 0)
        {
            sb.AppendLine("=== CHARACTERS ===");
            foreach (var ch in charsWithBios)
            {
                sb.AppendLine($"{ch.Name} ({ch.Class} Level {ch.Level}): {ch.Background}");
            }
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    public async Task<List<PlotThread>> FindSimilarPlotThreadsAsync(Guid gameId, float[] queryEmbedding, int topK = 5, CancellationToken ct = default)
    {
        if (queryEmbedding.Length == 0)
            return [];

        var queryVector = new Vector(queryEmbedding);
        return await db.PlotThreads
            .Where(t => t.GameId == gameId && t.Status == PlotThreadStatus.Active && t.Embedding != null)
            .OrderBy(t => t.Embedding!.CosineDistance(queryVector))
            .Take(topK)
            .ToListAsync(ct);
    }

    public async Task<string> GenerateSessionSummaryAsync(Guid gameId, Guid sessionId, CancellationToken ct = default)
    {
        var (provider, opts) = await GetProviderAsync(gameId, temperature: 0.5f, maxTokens: 1024, ct);
        if (provider is null)
            return "No LLM preset configured for this game.";

        var messages = await db.Messages
            .Where(m => m.SessionId == sessionId && !m.IsOOC)
            .OrderBy(m => m.CreatedAt)
            .Take(100)
            .Include(m => m.Player)
            .ToListAsync(ct);

        if (messages.Count == 0)
            return "No messages in this session.";

        var transcript = string.Join("\n", messages.Select(m =>
            $"{m.Player?.CharacterName ?? "GM"}: {m.Content}"));

        const string system = "You are a narrative scribe. Write a concise 'previously on...' recap of the game session below. Write in past tense, 3-5 sentences, focusing on the most dramatically important moments.";
        return await provider.CompleteAsync(system, transcript, opts, ct);
    }

    public async Task<ConsistencyReport> CheckPlotConsistencyAsync(Guid gameId, CancellationToken ct = default)
    {
        var (provider, opts) = await GetProviderAsync(gameId, temperature: 0.3f, maxTokens: 512, ct);
        if (provider is null)
            return new ConsistencyReport(true, [], "No LLM preset configured.");

        var context = await GeneratePlotContextAsync(gameId, ct);
        const string system = "You are a story consistency checker. Analyze the game state and identify any narrative contradictions or continuity issues. Respond with JSON: {\"isConsistent\": bool, \"issues\": [\"...\"], \"summary\": \"...\"}";

        var response = await provider.CompleteAsync(system, context, opts, ct);

        if (JsonExtract.TryExtractObject(response, out var obj))
        {
            var issues = new List<string>();
            if (obj.TryGetProperty("issues", out var issuesProp))
                issues = issuesProp.EnumerateArray().Select(i => i.GetString() ?? string.Empty).Where(s => s.Length > 0).ToList();
            var isConsistent = obj.TryGetProperty("isConsistent", out var ic) && ic.GetBoolean();
            var summary = obj.TryGetProperty("summary", out var s) ? s.GetString() ?? string.Empty : string.Empty;
            return new ConsistencyReport(isConsistent, issues, summary);
        }

        return new ConsistencyReport(true, [], response);
    }

    public async Task<PlotContinuation> SuggestContinuationAsync(Guid gameId, CancellationToken ct = default)
    {
        var (provider, opts) = await GetProviderAsync(gameId, temperature: 0.8f, maxTokens: 512, ct);
        if (provider is null)
            return new PlotContinuation("No LLM preset configured.", []);

        var context = await GeneratePlotContextAsync(gameId, ct);
        const string system = "You are a TTRPG game master advisor. Based on the current game state, suggest how the story could continue. Respond with JSON: {\"suggestion\": \"...\", \"possibleDirections\": [\"...\", \"...\", \"...\"]}";

        var response = await provider.CompleteAsync(system, context, opts, ct);

        if (JsonExtract.TryExtractObject(response, out var obj))
        {
            var suggestion = obj.TryGetProperty("suggestion", out var s) ? s.GetString() ?? string.Empty : string.Empty;
            var directions = new List<string>();
            if (obj.TryGetProperty("possibleDirections", out var dp))
                directions = dp.EnumerateArray().Select(d => d.GetString() ?? string.Empty).Where(s => s.Length > 0).ToList();
            return new PlotContinuation(suggestion, directions);
        }

        return new PlotContinuation(response, []);
    }

    public async Task EmbedMessagesAsync(Guid gameId, CancellationToken ct = default)
    {
        var session = await db.GameSessions
            .Where(s => s.GameId == gameId && s.Status == GameSessionStatus.Active)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (session is null) return;

        var messages = await db.Messages
            .Where(m => m.SessionId == session.Id && m.Embedding == null)
            .Take(50)
            .ToListAsync(ct);

        foreach (var msg in messages)
        {
            var embedding = await GetCachedEmbeddingAsync(msg.Content, gameId, ct);
            if (embedding.Length > 0)
                msg.Embedding = new Vector(embedding);
        }

        if (messages.Count > 0)
            await db.SaveChangesAsync(ct);
    }

    public async Task EmbedMessageAsync(Guid messageId, CancellationToken ct = default)
    {
        var message = await db.Messages
            .Include(m => m.Session)
            .FirstOrDefaultAsync(m => m.Id == messageId, ct);

        if (message is null || message.Embedding is not null) return;

        var embedding = await GetCachedEmbeddingAsync(message.Content, message.Session.GameId, ct);
        if (embedding.Length > 0)
        {
            message.Embedding = new Vector(embedding);
            await db.SaveChangesAsync(ct);
        }
    }

    private async Task<float[]> GetCachedEmbeddingAsync(string text, Guid gameId, CancellationToken ct)
    {
        var cacheKey = $"{gameId}:{text.GetHashCode()}";

        if (_embeddingCache.TryGetValue(cacheKey, out var cached))
        {
            if (DateTimeOffset.UtcNow - cached.CachedAt < CacheTtl)
                return cached.Embedding;
            _embeddingCache.TryRemove(cacheKey, out _);
        }

        var embedding = await embeddingService.GetEmbeddingAsync(text, gameId, ct);
        if (embedding.Length > 0)
            _embeddingCache[cacheKey] = (embedding, DateTimeOffset.UtcNow);

        return embedding;
    }

    private async Task<(ILLMProvider? Provider, LLMOptions Opts)> GetProviderAsync(
        Guid gameId, float temperature, int maxTokens, CancellationToken ct)
    {
        var game = await db.Games
            .Include(g => g.LLMPreset)
            .FirstOrDefaultAsync(g => g.Id == gameId, ct);

        if (game?.LLMPreset is null)
            return (null, default!);

        var preset = game.LLMPreset;
        if (preset.ApiKey is not null)
            preset.DecryptedApiKey = encryption.Decrypt(preset.ApiKey);

        var opts = new LLMOptions
        {
            Model = preset.BaseModel,
            Temperature = temperature,
            MaxTokens = maxTokens,
            TopP = preset.TopP
        };

        return (factory.CreateFromPreset(preset), opts);
    }
}
