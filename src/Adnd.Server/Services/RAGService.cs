using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pgvector;
using Adnd.Server.Data;
using Adnd.Server.Models;

namespace Adnd.Server.Services;

/// <summary>
/// Retrieval-Augmented Generation service for maintaining plot consistency
/// and retrieving relevant context from game history.
/// </summary>
public interface IRAGService
{
    /// <summary>
    /// Generate plot context for the current game state.
    /// Retrieves relevant plot threads, NPCs, and recent messages.
    /// </summary>
    Task<string> GeneratePlotContextAsync(Guid gameId, int maxMessages = 20);

    /// <summary>
    /// Find similar plot threads using vector similarity search.
    /// </summary>
    Task<List<PlotThread>> FindSimilarPlotThreadsAsync(Guid gameId, string query, int limit = 5);

    /// <summary>
    /// Generate a GM summary of recent game events.
    /// </summary>
    Task<string> GenerateSessionSummaryAsync(Guid sessionId, int messageCount = 30);

    /// <summary>
    /// Check for plot consistency (detect contradictions in recent events).
    /// </summary>
    Task<ConsistencyReport> CheckPlotConsistencyAsync(Guid gameId, int messageCount = 50);

    /// <summary>
    /// Generate a continuation suggestion for the current plot thread.
    /// </summary>
    Task<PlotContinuation> SuggestContinuationAsync(Guid plotThreadId, string currentContext);

    /// <summary>
    /// Generate and store embeddings for messages in a session.
    /// Only generates embeddings for narrative-influencing messages (not OOC).
    /// </summary>
    Task EmbedMessagesAsync(Guid gameId, IEnumerable<Guid> messageIds);

    /// <summary>
    /// Generate and store an embedding for a single message.
    /// </summary>
    Task EmbedMessageAsync(Guid messageId, string content);
}

public class RAGService : IRAGService
{
    private readonly AppDbContext _context;
    private readonly ILLMProviderFactory _providerFactory;
    private readonly IEmbeddingService _embeddingService;
    private readonly ILogger<RAGService> _logger;

    public RAGService(
        AppDbContext context,
        ILLMProviderFactory providerFactory,
        IEmbeddingService embeddingService,
        ILogger<RAGService> logger)
    {
        _context = context;
        _providerFactory = providerFactory;
        _embeddingService = embeddingService;
        _logger = logger;
    }

    public async Task<string> GeneratePlotContextAsync(Guid gameId, int maxMessages = 20)
    {
        var game = await _context.Games
            .Include(g => g.NPCs)
            .Include(g => g.Sessions)
            .FirstOrDefaultAsync(g => g.Id == gameId);

        if (game == null)
            return "No game data found.";

        var contextParts = new List<string>();

        // Game metadata
        contextParts.Add($"## Game: {game.Name}");
        contextParts.Add($"System: {game.SystemId ?? "unknown"} v{game.SystemVersion ?? "unknown"}");
        contextParts.Add($"Status: {game.Status}");
        contextParts.Add($"Created: {game.CreatedAt:yyyy-MM-dd}");
        contextParts.Add($"Language: {game.Language ?? "English"}");

        // Active plot threads
        var activeThreads = await _context.PlotThreads
            .Where(p => p.GameId == gameId && p.Status == PlotThreadStatus.Active)
            .ToListAsync();

        if (activeThreads.Any())
        {
            contextParts.Add("\n## Active Plot Threads:");
            foreach (var thread in activeThreads)
            {
                contextParts.Add($"- **{thread.Title}**: {thread.Description}");
            }
        }

        // NPCs
        if (game.NPCs.Any())
        {
            contextParts.Add("\n## NPCs:");
            foreach (var npc in game.NPCs.Take(10))
            {
                contextParts.Add($"- **{npc.Name}**: {npc.Description ?? "No description"}");
            }
        }

        // Recent messages
        var recentMessages = await _context.Messages
            .Where(m => m.Session!.GameId == gameId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(maxMessages)
            .ToListAsync();

        if (recentMessages.Any())
        {
            contextParts.Add($"\n## Recent Events (last {Math.Min(maxMessages, recentMessages.Count)} messages):");
            foreach (var msg in recentMessages.OrderByDescending(m => m.CreatedAt).Take(10))
            {
                var playerInfo = msg.Player != null ? $" ({msg.Player.User?.DisplayName ?? msg.Player.CharacterName})" : "(System)";
                contextParts.Add($"[{msg.CreatedAt:HH:mm}] [{msg.Type}] {playerInfo}: {msg.Content}");
            }
        }

        return string.Join("\n", contextParts);
    }

    public async Task<List<PlotThread>> FindSimilarPlotThreadsAsync(Guid gameId, string query, int limit = 5)
    {
        // Generate embedding for the query using the game's preset embedding model
        float[]? queryEmbedding = null;
        try
        {
            queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(gameId, query);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate embedding for query in game {GameId}, falling back to keyword search", gameId);
        }

        // Try vector similarity search first
        if (queryEmbedding != null && queryEmbedding.Length > 0)
        {
            try
            {
                using var conn = _context.Database.GetDbConnection();
                await conn.OpenAsync();

                using var cmd = new Npgsql.NpgsqlCommand(@"
                    SELECT id, title, description, status, created_at, updated_at
                    FROM plot_threads
                    WHERE game_id = @gameId
                    ORDER BY embedding <=> @query_embedding
                    LIMIT @limit", (Npgsql.NpgsqlConnection)(object)conn);
                cmd.Parameters.AddWithValue("gameId", gameId.ToString());
                cmd.Parameters.AddWithValue("query_embedding", queryEmbedding);
                cmd.Parameters.AddWithValue("limit", limit);

                using var reader = await cmd.ExecuteReaderAsync();
                var results = new List<PlotThread>();

                while (await reader.ReadAsync())
                {
                    results.Add(new PlotThread
                    {
                        Id = reader.GetGuid(0),
                        Title = reader.GetString(1),
                        Description = reader.GetString(2),
                        Status = (PlotThreadStatus)reader.GetInt32(3),
                        CreatedAt = reader.GetDateTime(4),
                        UpdatedAt = reader.IsDBNull(5) ? null : reader.GetDateTime(5)
                    });
                }

                if (results.Any())
                    return results;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Vector search failed, falling back to keyword search");
            }
        }

        // Fallback: keyword search
        return await _context.PlotThreads
            .Where(p => p.GameId == gameId && p.Status == PlotThreadStatus.Active)
            .Where(p => EF.Functions.Like(p.Title, $"%{query}%") ||
                        EF.Functions.Like(p.Description, $"%{query}%"))
            .OrderByDescending(p => p.CreatedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<string> GenerateSessionSummaryAsync(Guid sessionId, int messageCount = 30)
    {
        var messages = await _context.Messages
            .Where(m => m.SessionId == sessionId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(messageCount)
            .ToListAsync();

        if (!messages.Any())
            return "No messages to summarize.";

        var summaryParts = new List<string> { "## Session Summary\n" };

        // Group by type (map old enum values to new ones)
        var chatMsgs = messages.Where(m => m.Type == MessageType.InGamePublic).ToList();
        var actionMsgs = messages.Where(m => m.Type == MessageType.Action).ToList();
        var diceMsgs = messages.Where(m => m.Type == MessageType.Dice).ToList();
        var gmMsgs = messages.Where(m => m.Type == MessageType.GM).ToList();

        if (gmMsgs.Any())
        {
            summaryParts.Add("### GM Announcements");
            foreach (var msg in gmMsgs.Take(5))
                summaryParts.Add($"- {msg.Content}");
        }

        if (actionMsgs.Any())
        {
            summaryParts.Add("\n### Actions Taken");
            foreach (var msg in actionMsgs.Take(5))
                summaryParts.Add($"- {msg.Content}");
        }

        if (diceMsgs.Any())
        {
            summaryParts.Add("\n### Key Dice Rolls");
            foreach (var msg in diceMsgs.Take(5))
                summaryParts.Add($"- {msg.Content}");
        }

        if (chatMsgs.Any())
        {
            summaryParts.Add("\n### Notable Chat");
            foreach (var msg in chatMsgs.Take(5))
            {
                var playerName = msg.Player?.User?.DisplayName ?? msg.Player?.CharacterName ?? "Unknown";
                summaryParts.Add($"- [{playerName}]: {msg.Content}");
            }
        }

        return string.Join("\n", summaryParts);
    }

    public async Task<ConsistencyReport> CheckPlotConsistencyAsync(Guid gameId, int messageCount = 50)
    {
        var messages = await _context.Messages
            .Where(m => m.Session!.GameId == gameId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(messageCount)
            .ToListAsync();

        var report = new ConsistencyReport
        {
            GameId = gameId,
            MessagesAnalyzed = messages.Count,
            CheckedAt = DateTime.UtcNow,
            Warnings = new List<string>(),
            Findings = new List<string>()
        };

        // Check for HP inconsistencies (character HP going up and down without healing)
        var hpChanges = messages
            .Where(m => m.Type == MessageType.Action)
            .Where(m => m.Content.Contains("HP", StringComparison.OrdinalIgnoreCase))
            .Select(m => m.Content)
            .ToList();

        if (hpChanges.Count > 3)
        {
            report.Warnings.Add("Multiple HP changes detected in recent messages. Verify damage/healing totals.");
        }

        // Check for location inconsistencies
        var locationMsgs = messages
            .Where(m => m.Content.Contains("location:", StringComparison.OrdinalIgnoreCase) ||
                        m.Content.Contains("at ", StringComparison.OrdinalIgnoreCase))
            .Select(m => m.Content)
            .ToList();

        // Check for NPC death/resurrection
        var deathMsgs = messages
            .Where(m => m.Content.Contains("dies", StringComparison.OrdinalIgnoreCase) ||
                        m.Content.Contains("killed", StringComparison.OrdinalIgnoreCase) ||
                        m.Content.Contains("resurrect", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (deathMsgs.Count > 2)
        {
            report.Warnings.Add($"Multiple death/resurrection events detected: {deathMsgs.Count} messages.");
        }

        // Check for time inconsistencies
        var timeMsgs = messages
            .Where(m => m.Content.Contains("time passes", StringComparison.OrdinalIgnoreCase) ||
                        m.Content.Contains("hours later", StringComparison.OrdinalIgnoreCase) ||
                        m.Content.Contains("days later", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (timeMsgs.Any())
        {
            report.Findings.Add($"Time progression noted: {timeMsgs.Count} time skip messages.");
        }

        return report;
    }

    public async Task<PlotContinuation> SuggestContinuationAsync(Guid plotThreadId, string currentContext)
    {
        // Get the game from the plot thread
        var thread = await _context.PlotThreads.FindAsync(plotThreadId);
        if (thread == null)
        {
            _logger.LogWarning("Plot thread {PlotThreadId} not found.", plotThreadId);
            return new PlotContinuation
            {
                Suggestions = new[] { "Plot thread not found." },
                Generated = false,
                Reason = "Plot thread not found"
            };
        }

        var game = await _context.Games
            .Include(g => g.LLMPreset)
            .FirstOrDefaultAsync(g => g.Id == thread.GameId);

        if (game == null || game.LLMPreset == null)
        {
            _logger.LogWarning("No LLM preset configured for game {GameId}.", thread.GameId);
            return new PlotContinuation
            {
                Suggestions = new[]
                {
                    "Consider having the NPCs react to the players' recent actions.",
                    "Introduce a new complication related to the current plot thread.",
                    "Offer the players a choice between two interesting paths.",
                    "Reveal a hidden detail about the current location or situation."
                },
                Generated = false,
                Reason = "No LLM preset configured"
            };
        }

        // Provider is created from preset at runtime via the factory
        // (no need to check registry — if preset exists, factory can create provider)
        var provider = _providerFactory.CreateFromPreset(game.LLMPreset);

        try
        {
            var systemPrompt = "You are a creative and helpful TTRPG Game Master assistant.\\n" +
                "Your job is to suggest engaging plot continuations based on the game context.\\n" +
                "Be concise and provide actionable suggestions.\\n" +
                "Format your response as JSON with the following structure:\\n" +
                "{\\n" +
                "  \"suggestions\": [\"suggestion 1\", \"suggestion 2\", \"suggestion 3\"],\\n" +
                "  \"tone\": \"suspenseful|comedic|dark|hopeful|adventurous\",\\n" +
                "  \"difficulty\": \"easy|medium|hard\"\\n" +
                "}";

            var result = await provider.CompleteStructuredAsync<PlotContinuation>(
                systemPrompt,
                $"Current plot context:\n{currentContext}\n\nSuggest 3 engaging plot continuations.",
                new LLMOptions { Temperature = 0.8f, MaxTokens = 1024 }
            );

            return result ?? new PlotContinuation
            {
                Suggestions = new[] { "No suggestions available." },
                Generated = false,
                Reason = "Deserialization failed"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate plot continuation");
            return new PlotContinuation
            {
                Suggestions = new[] { "LLM error: " + ex.Message },
                Generated = false,
                Reason = ex.Message
            };
        }
    }

    public async Task EmbedMessagesAsync(Guid gameId, IEnumerable<Guid> messageIds)
    {
        var ids = messageIds.ToList();
        if (!ids.Any())
            return;

        // Get messages that don't have embeddings yet, only narrative-influencing ones
        var messages = await _context.Messages
            .Where(m => ids.Contains(m.Id) && m.Embedding == null && !m.IsOOC)
            .ToListAsync();

        if (!messages.Any())
            return;

        try
        {
            var texts = messages.Select(m => m.Content).ToList();
            var embeddings = await _embeddingService.GenerateEmbeddingsAsync(gameId, texts);

            for (var i = 0; i < messages.Count; i++)
            {
                messages[i].Embedding = embeddings[i] != null ? new Vector(embeddings[i]) : null;
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Generated {Count} embeddings for messages in game {GameId}", messages.Count, gameId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate embeddings for {Count} messages in game {GameId}", messages.Count, gameId);
        }
    }

    public async Task EmbedMessageAsync(Guid messageId, string content)
    {
        var message = await _context.Messages.FindAsync(messageId);
        if (message == null || message.Embedding != null || message.IsOOC)
            return;

        var session = await _context.GameSessions.FindAsync(message.SessionId);
        if (session == null)
            return;

        try
        {
            var embedding = await _embeddingService.GenerateEmbeddingAsync(session.GameId, content);
            message.Embedding = embedding != null ? new Vector(embedding) : null;
            await _context.SaveChangesAsync();
            _logger.LogDebug("Generated embedding for message {MessageId}", messageId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate embedding for message {MessageId}", messageId);
        }
    }
}

public class ConsistencyReport
{
    public Guid GameId { get; set; }
    public int MessagesAnalyzed { get; set; }
    public DateTime CheckedAt { get; set; }
    public List<string> Warnings { get; set; } = new();
    public List<string> Findings { get; set; } = new();
}

public class PlotContinuation
{
    public string[] Suggestions { get; set; } = Array.Empty<string>();
    public string? Tone { get; set; }
    public string? Difficulty { get; set; }
    public bool Generated { get; set; }
    public string? Reason { get; set; }
}
