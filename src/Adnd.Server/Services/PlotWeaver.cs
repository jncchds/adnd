using System.Text.Json;
using Adnd.Server.Data;
using Adnd.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Services;

/// <summary>
/// PlotWeaver — automatically generates, maintains, and evolves plot threads
/// based on game events. Fully automatic: no manual triggers needed.
/// </summary>
public interface IPlotWeaver
{
    /// <summary>
    /// Generate initial plot threads from game creation inputs.
    /// Called once when the game starts.
    /// </summary>
    Task<List<PlotThread>> GenerateInitialThreadsAsync(
        Guid gameId, string premise, string parameters, string systemId, Guid? presetId);

    /// <summary>
    /// Review all active plot threads and adapt them based on recent game events.
    /// Called automatically on significant events.
    /// </summary>
    Task<PlotReview> ReviewAndAdaptAsync(Guid gameId, string context, string trigger);

    /// <summary>
    /// Adjust momentum for a specific thread after an event.
    /// Lightweight — does not call the LLM.
    /// </summary>
    Task UpdateMomentumAsync(Guid gameId, Guid threadId, float delta, string reason);

    /// <summary>
    /// Get prioritized active threads for display.
    /// </summary>
    Task<List<PlotThread>> GetActiveThreadsAsync(Guid gameId);

    /// <summary>
    /// Get review history for a game.
    /// </summary>
    Task<List<PlotReview>> GetReviewHistoryAsync(Guid gameId, int limit = 20);

    /// <summary>
    /// Check if initial threads have been generated for a game.
    /// </summary>
    Task<bool> HasInitialThreadsAsync(Guid gameId);

    // ==================== Dynamic Generation ====================

    /// <summary>
    /// Generate new plot threads from recent game context.
    /// Called during reviews when the LLM detects story opportunities.
    /// </summary>
    Task<List<PlotThread>> GenerateNewThreadsAsync(Guid gameId, string context, string trigger);

    /// <summary>
    /// Spawn milestone events for threads whose momentum is high enough.
    /// Called when a thread's momentum crosses the milestone threshold.
    /// </summary>
    Task<List<MilestoneEvent>> SpawnMilestonesAsync(Guid gameId);

    /// <summary>
    /// Detect story opportunities from recent game events.
    /// Returns a list of suggested actions (new threads, milestones, adaptations).
    /// </summary>
    Task<List<StoryOpportunity>> DetectOpportunitiesAsync(Guid gameId, string context);
}

/// <summary>
/// A story opportunity detected by PlotWeaver.
/// </summary>
public class StoryOpportunity
{
    public OpportunityType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? ThreadId { get; set; }
    public float? MomentumDelta { get; set; }
    public string? NewThreadCategory { get; set; }
    public string? NewThreadTitle { get; set; }
    public string? NewThreadDescription { get; set; }
    public string? NewMilestone { get; set; }
}

public enum OpportunityType
{
    NewThread,
    SpawnMilestone,
    AdaptThread,
    MergeThreads,
    EscalateThreat
}

public class PlotWeaver : IPlotWeaver
{
    private readonly AppDbContext _context;
    private readonly ILLMProviderRegistry _llmRegistry;
    private readonly IRAGService _ragService;
    private readonly ILogger<PlotWeaver> _logger;

    public PlotWeaver(
        AppDbContext context,
        ILLMProviderRegistry llmRegistry,
        IRAGService ragService,
        ILogger<PlotWeaver> logger)
    {
        _context = context;
        _llmRegistry = llmRegistry;
        _ragService = ragService;
        _logger = logger;
    }

    /// <summary>
    /// Get an LLM provider — first from registry (built-in), then from preset.
    /// </summary>
    private ILLMProvider GetProvider(LLMPreset preset)
    {
        var provider = _llmRegistry.GetProvider(preset.ProviderType);
        if (provider != null) return provider;

        return preset.ProviderType switch
        {
            "ollama" => new OllamaLLMProviderFromPreset(preset),
            "lmstudio" => new LmStudioLLMProviderFromPreset(preset),
            "openai" => new OpenAILLMProviderFromPreset(preset),
            "google" => new GoogleAIStudioLLMProviderFromPreset(preset),
            _ => null
        };
    }

    public async Task<bool> HasInitialThreadsAsync(Guid gameId)
    {
        return await _context.PlotThreads.AnyAsync(t => t.GameId == gameId);
    }

    public async Task<List<PlotThread>> GenerateInitialThreadsAsync(
        Guid gameId, string premise, string parameters, string systemId, Guid? presetId)
    {
        var game = await _context.Games
            .Include(g => g.LLMPreset)
            .FirstOrDefaultAsync(g => g.Id == gameId);

        if (game == null)
            throw new KeyNotFoundException($"Game {gameId} not found.");

        if (game.LLMPreset == null)
            throw new InvalidOperationException("Cannot generate threads: no LLM preset configured.");

        var provider = GetProvider(game.LLMPreset);
        if (provider == null)
            throw new InvalidOperationException($"LLM provider '{game.LLMPreset.ProviderType}' not available.");

        var systemPrompt =
            "You are a TTRPG plot architect. Given a game's premise, tone, and RPG system,\n" +
            "generate 3-5 compelling plot threads that will guide the story. Each thread should have:\n" +
            "- A title (short, evocative)\n" +
            "- A category (Faction, Mystery, Personal, Threat, WorldEvent, Relationship)\n" +
            "- A description (2-3 sentences of what this thread is about)\n" +
            "- A next milestone (the first event that should happen in this thread)\n" +
            "- A foreshadowing hint (a clue or hint that can be planted for players)\n" +
            "\n" +
            "Respond with ONLY a JSON array in this exact format (no markdown, no extra text):\n" +
            "[\n" +
            "  {\n" +
            "    \"title\": \"...\",\n" +
            "    \"category\": \"Faction\",\n" +
            "    \"description\": \"...\",\n" +
            "    \"nextMilestone\": \"...\",\n" +
            "    \"foreshadowing\": \"...\"\n" +
            "  },\n" +
            "  ...\n" +
            "]" +
            (string.IsNullOrEmpty(game.Language) || game.Language == "English" ? "" :
                $"\n\n**Language**: All plot thread titles, descriptions, and milestones must be in **{game.Language}**. Write all JSON content in {game.Language}.");

        var userPrompt =
            $"Game system: {systemId}\n" +
            $"Premise: {premise}\n" +
            $"Parameters: {parameters}\n" +
            "\n" +
            "Generate 3-5 plot threads for this game. Make them interwoven and compelling.\n" +
            "The threads should create a web of interconnected storylines that the GM can follow\n" +
            "and that will evolve as players make choices.";

        try
        {
            var result = await provider.CompleteAsync(systemPrompt, userPrompt, new LLMOptions
            {
                Temperature = 0.8f,
                MaxTokens = 2048
            });

            // Parse JSON — strip markdown code fences if present
            var json = result.Trim();
            if (json.StartsWith("```"))
            {
                var lines = json.Split('\n');
                json = string.Join('\n', lines.Skip(1).Take(lines.Length - 2));
            }

            var threads = JsonSerializer.Deserialize<List<ThreadTemplate>>(json);
            if (threads == null || !threads.Any())
                throw new InvalidOperationException("LLM returned no plot threads.");

            // Generate embeddings for each thread
            var embeddings = await GenerateEmbeddings(threads.Select(t => t.title + " " + t.description).ToList());

            var plotThreads = new List<PlotThread>();
            foreach (var (thread, embedding) in threads.Zip(embeddings))
            {
                var plotThread = new PlotThread
                {
                    GameId = gameId,
                    Title = thread.title,
                    Category = ParseCategory(thread.category),
                    Description = thread.description,
                    NextMilestone = thread.nextMilestone,
                    Foreshadowing = thread.foreshadowing,
                    Momentum = 0f,
                    RelevanceScore = 0.5f,
                    Status = PlotThreadStatus.Active,
                    Embedding = embedding
                };

                _context.PlotThreads.Add(plotThread);
                plotThreads.Add(plotThread);
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Generated {Count} initial plot threads for game {GameId}",
                plotThreads.Count, gameId);

            return plotThreads;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate initial plot threads for game {GameId}", gameId);
            throw;
        }
    }

    public async Task<PlotReview> ReviewAndAdaptAsync(Guid gameId, string context, string trigger)
    {
        var game = await _context.Games
            .Include(g => g.LLMPreset)
            .FirstOrDefaultAsync(g => g.Id == gameId);

        if (game == null)
            throw new KeyNotFoundException($"Game {gameId} not found.");

        if (game.LLMPreset == null)
            throw new InvalidOperationException("Cannot review: no LLM preset configured.");

        var provider = GetProvider(game.LLMPreset);
        if (provider == null)
            throw new InvalidOperationException($"LLM provider '{game.LLMPreset.ProviderType}' not available.");

        var threads = await _context.PlotThreads
            .Where(t => t.GameId == gameId && t.Status == PlotThreadStatus.Active)
            .ToListAsync();

        if (!threads.Any())
        {
            _logger.LogWarning("No active plot threads to review for game {GameId}", gameId);
            return new PlotReview { GameId = gameId, Trigger = trigger, Summary = "No active threads." };
        }

        var systemPrompt = @"You are a TTRPG plot architect reviewing the current state of a game's story.
The players have made choices that have shifted the narrative. Review each plot thread and decide:

1. **Momentum**: How urgent is this thread now? Scale from -10 (abandoned/fading) to +10 (urgent/critical).
2. **Status**: Keep Active, or mark as Resolved (if the thread's goal is achieved) or Abandoned (if no longer relevant).
3. **Description**: Update the description to reflect how events have changed this thread.
4. **Next Milestone**: Update the next expected event based on what has happened.
5. **Relevance**: How relevant is this thread to the current game state? 0-1 scale.

Respond with ONLY a JSON array in this exact format (no markdown, no extra text):
[
  {
    ""threadId"": ""uuid"",
    ""momentum"": 5,
    ""status"": ""Active"",
    ""description"": ""Updated description"",
    ""nextMilestone"": ""Next expected event"",
    ""relevance"": 0.8,
    ""reason"": ""Why these changes""
  },
  ...
]

Only include threads that need changes. Threads not listed keep their current values." +
            (string.IsNullOrEmpty(game.Language) || game.Language == "English" ? "" :
                $"\n\n**Language**: All plot thread descriptions, titles, and milestones in the JSON output must be in **{game.Language}**. Write all JSON content in {game.Language}.");

        var threadList = string.Join("\n", threads.Select(t =>
            $"- [{t.Category}] {t.Title} (momentum: {t.Momentum:F1}, relevance: {t.RelevanceScore:F2})\n  {t.Description}\n  Next: {t.NextMilestone}"));

        var userPrompt = $"Review these plot threads in light of recent game events.\n\n" +
            $"Active threads:\n{threadList}\n\n" +
            $"Recent game context:\n{context}\n\n" +
            "Update the threads based on what has happened. Be creative and responsive to player choices.";

        try
        {
            var result = await provider.CompleteAsync(systemPrompt, userPrompt, new LLMOptions
            {
                Temperature = 0.7f,
                MaxTokens = 3072
            });

            // Parse JSON
            var json = result.Trim();
            if (json.StartsWith("```"))
            {
                var lines = json.Split('\n');
                json = string.Join('\n', lines.Skip(1).Take(lines.Length - 2));
            }

            var updates = JsonSerializer.Deserialize<List<ThreadAdaptation>>(json);
            if (updates == null || !updates.Any())
            {
                _logger.LogWarning("PlotWeaver review returned no adaptations for game {GameId}", gameId);
                return new PlotReview { GameId = gameId, Trigger = trigger, Summary = "No adaptations needed." };
            }

            var review = new PlotReview
            {
                GameId = gameId,
                Trigger = trigger,
                ReviewedAt = DateTime.UtcNow
            };

            var summaryParts = new List<string>();

            foreach (var update in updates)
            {
                var thread = threads.FirstOrDefault(t => t.Id.ToString() == update.threadId);
                if (thread == null)
                    continue;

                var threadUpdate = new ThreadUpdate
                {
                    ThreadId = thread.Id,
                    ThreadTitle = thread.Title,
                    OldMomentum = thread.Momentum,
                    NewMomentum = update.momentum,
                    OldStatus = thread.Status.ToString(),
                    NewStatus = update.status,
                    OldDescription = thread.Description,
                    NewDescription = update.description,
                    OldMilestone = thread.NextMilestone,
                    NewMilestone = update.nextMilestone,
                    Reason = update.reason
                };

                thread.Momentum = update.momentum;
                thread.Status = ParseStatus(update.status);
                thread.Description = update.description;
                thread.NextMilestone = update.nextMilestone;
                thread.RelevanceScore = update.relevance;
                thread.UpdatedAt = DateTime.UtcNow;
                thread.AdaptationHistory.Add(update.reason);

                review.Updates.Add(threadUpdate);
                summaryParts.Add($"**{thread.Title}**: momentum {threadUpdate.OldMomentum:F1}→{threadUpdate.NewMomentum:F1} ({threadUpdate.Reason})");
            }

            review.Summary = string.Join("; ", summaryParts);

            await _context.SaveChangesAsync();

            _logger.LogInformation("PlotWeaver review for game {GameId}: {Summary}", gameId, review.Summary);

            return review;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to review plot threads for game {GameId}", gameId);
            return new PlotReview
            {
                GameId = gameId,
                Trigger = trigger,
                Summary = $"Review failed: {ex.Message}"
            };
        }
    }

    public async Task UpdateMomentumAsync(Guid gameId, Guid threadId, float delta, string reason)
    {
        var thread = await _context.PlotThreads.FindAsync(threadId);
        if (thread == null || thread.GameId != gameId)
            throw new KeyNotFoundException($"Plot thread {threadId} not found for game {gameId}.");

        var oldMomentum = thread.Momentum;
        thread.Momentum = Math.Max(-10f, Math.Min(10f, thread.Momentum + delta));

        // Auto-resolve very high momentum threads
        if (thread.Momentum >= 9f && thread.Status == PlotThreadStatus.Active)
        {
            thread.Status = PlotThreadStatus.Resolved;
            thread.UpdatedAt = DateTime.UtcNow;
        }

        // Auto-abandon very low momentum threads
        if (thread.Momentum <= -8f && thread.Status == PlotThreadStatus.Active)
        {
            thread.Status = PlotThreadStatus.Abandoned;
            thread.UpdatedAt = DateTime.UtcNow;
        }

        thread.AdaptationHistory.Add($"{reason} (momentum {oldMomentum:F1} → {thread.Momentum:F1})");
        thread.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogDebug("Updated momentum for thread '{ThreadTitle}' in game {GameId}: {Old} → {New} ({Reason})",
            thread.Title, gameId, oldMomentum, thread.Momentum, reason);
    }

    public async Task<List<PlotThread>> GetActiveThreadsAsync(Guid gameId)
    {
        var threads = await _context.PlotThreads
            .Where(t => t.GameId == gameId)
            .OrderByDescending(t => t.RelevanceScore)
            .ThenByDescending(t => t.Momentum)
            .ToListAsync();

        // Compute relevance scores dynamically
        await ComputeRelevanceScores(gameId, threads);

        return threads;
    }

    public async Task<List<PlotReview>> GetReviewHistoryAsync(Guid gameId, int limit = 20)
    {
        return await _context.PlotReviews
            .Where(r => r.GameId == gameId)
            .OrderByDescending(r => r.ReviewedAt)
            .Take(limit)
            .ToListAsync();
    }

    /// <summary>
    /// Compute relevance scores for threads based on momentum and recency.
    /// </summary>
    private async Task ComputeRelevanceScores(Guid gameId, List<PlotThread> threads)
    {
        var now = DateTime.UtcNow;
        foreach (var thread in threads)
        {
            if (thread.Status == PlotThreadStatus.Active)
            {
                // Relevance = momentum weight + recency decay
                var momentumWeight = (thread.Momentum + 10f) / 20f; // 0-1 from momentum
                var recency = thread.UpdatedAt.HasValue
                    ? (float)Math.Max(0, 1 - (now - thread.UpdatedAt.Value).TotalHours / 72) // 3-day half-life
                    : 0.5f;
                thread.RelevanceScore = momentumWeight * 0.6f + recency * 0.4f;
            }
            else
            {
                thread.RelevanceScore = 0;
            }
        }

        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Generate pgvector embeddings for a list of texts.
    /// </summary>
    private async Task<List<float[]>> GenerateEmbeddings(List<string> texts)
    {
        // Find a game with an embedding-capable preset
        // For now, use the first game with an embedding model configured
        var preset = await _context.LLMPresets.FirstOrDefaultAsync(p => !string.IsNullOrEmpty(p.EmbeddingModel));
        if (preset == null)
        {
            _logger.LogWarning("No LLM preset with embedding model found. Skipping embeddings.");
            return texts.Select(_ => new float[1536]).ToList(); // Fallback: random-ish vectors
        }

        var provider = GetProvider(preset);
        if (provider == null)
        {
            _logger.LogWarning("LLM provider '{Provider}' not available for embeddings.", preset.ProviderType);
            return texts.Select(_ => new float[1536]).ToList();
        }

        try
        {
            var results = new List<float[]>();
            foreach (var text in texts)
            {
                var embedding = await provider.GetEmbeddingAsync(text);
                results.Add(embedding);
            }
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate embeddings, using fallback vectors");
            return texts.Select(_ => new float[1536]).ToList();
        }
    }

    // ==================== Dynamic Generation ====================

    public async Task<List<PlotThread>> GenerateNewThreadsAsync(Guid gameId, string context, string trigger)
    {
        var game = await _context.Games
            .Include(g => g.LLMPreset)
            .FirstOrDefaultAsync(g => g.Id == gameId);

        if (game == null)
            throw new KeyNotFoundException($"Game {gameId} not found.");

        if (game.LLMPreset == null)
            throw new InvalidOperationException("Cannot generate threads: no LLM preset configured.");

        var provider = GetProvider(game.LLMPreset);
        if (provider == null)
            throw new InvalidOperationException($"LLM provider '{game.LLMPreset.ProviderType}' not available.");

        var existingThreads = await _context.PlotThreads
            .Where(t => t.GameId == gameId && t.Status == PlotThreadStatus.Active)
            .ToListAsync();

        var systemPrompt =
        """
You are a TTRPG plot architect. New events in the game have created story opportunities.
Generate NEW plot threads that emerge from these events. Do NOT repeat existing threads.
Existing threads: {string.Join(", ", existingThreads.Select(t => t.Title))}

Respond with ONLY a JSON array in this exact format (no markdown, no extra text):
[
  {{
    "title": "...",
    "category": "Faction",
    "description": "...",
    "nextMilestone": "...",
    "foreshadowing": "..."
  }},
  ...
]

Only generate threads that are genuinely new and relevant to the current game state.
""" +
            (string.IsNullOrEmpty(game.Language) || game.Language == "English" ? "" :
                $"\n\n**Language**: All plot thread titles, descriptions, and milestones must be in **{game.Language}**. Write all JSON content in {game.Language}.");

        var userPrompt =
            $"Recent game events:\n{context}\n\n" +
            "What new plot threads have emerged from these events? Consider:\n" +
            "- New locations players have discovered\n" +
            "- NPCs they've met or killed\n" +
            "- Factions they've interacted with\n" +
            "- Personal stakes that have developed\n" +
            "- Consequences of their actions\n\n" +
            "Generate 1-3 new plot threads.";

        try
        {
            var result = await provider.CompleteAsync(systemPrompt, userPrompt, new LLMOptions
            {
                Temperature = 0.85f,
                MaxTokens = 2048
            });

            var json = result.Trim();
            if (json.StartsWith("```"))
            {
                var lines = json.Split('\n');
                json = string.Join('\n', lines.Skip(1).Take(lines.Length - 2));
            }

            var threads = JsonSerializer.Deserialize<List<ThreadTemplate>>(json);
            if (threads == null || !threads.Any())
                return new List<PlotThread>();

            var embeddings = await GenerateEmbeddings(threads.Select(t => t.title + " " + t.description).ToList());

            var plotThreads = new List<PlotThread>();
            foreach (var (thread, embedding) in threads.Zip(embeddings))
            {
                var plotThread = new PlotThread
                {
                    GameId = gameId,
                    Title = thread.title,
                    Category = ParseCategory(thread.category),
                    Description = thread.description,
                    NextMilestone = thread.nextMilestone,
                    Foreshadowing = thread.foreshadowing,
                    Momentum = 1f, // Start with slight positive momentum
                    RelevanceScore = 0.6f,
                    Status = PlotThreadStatus.Active,
                    IsDynamic = true,
                    Embedding = embedding
                };

                _context.PlotThreads.Add(plotThread);
                plotThreads.Add(plotThread);
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Generated {Count} new dynamic plot threads for game {GameId} (trigger: {Trigger})",
                plotThreads.Count, gameId, trigger);

            return plotThreads;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate new plot threads for game {GameId}", gameId);
            return new List<PlotThread>();
        }
    }

    public async Task<List<MilestoneEvent>> SpawnMilestonesAsync(Guid gameId)
    {
        var game = await _context.Games
            .Include(g => g.LLMPreset)
            .FirstOrDefaultAsync(g => g.Id == gameId);

        if (game == null)
            throw new KeyNotFoundException($"Game {gameId} not found.");

        if (game.LLMPreset == null)
            throw new InvalidOperationException("Cannot spawn milestones: no LLM preset configured.");

        var provider = GetProvider(game.LLMPreset);
        if (provider == null)
            throw new InvalidOperationException($"LLM provider '{game.LLMPreset.ProviderType}' not available.");

        // Find threads with high momentum that need milestones
        var highMomentumThreads = await _context.PlotThreads
            .Where(t => t.GameId == gameId &&
                        t.Status == PlotThreadStatus.Active &&
                        t.Momentum >= 5f &&
                        (t.NextMilestone == null || t.NextMilestone.Contains("[PENDING]")))
            .ToListAsync();

        if (!highMomentumThreads.Any())
        {
            _logger.LogDebug("No threads with sufficient momentum for milestone spawning in game {GameId}", gameId);
            return new List<MilestoneEvent>();
        }

        var systemPrompt =
        """
You are a TTRPG plot architect. Several plot threads have reached critical momentum and need concrete milestone events.
For each thread, generate a specific, actionable milestone event that should happen next.

Respond with ONLY a JSON array in this exact format (no markdown, no extra text):
[
  {{
    "threadId": "uuid",
    "milestoneTitle": "...",
    "milestoneDescription": "..."
  }},
  ...
]

The milestone should be a specific event (not a vague suggestion). Example: "The cult leader arrives at the village" not "Something bad happens with the cult."
""" +
            (string.IsNullOrEmpty(game.Language) || game.Language == "English" ? "" :
                $"\n\n**Language**: All milestone titles and descriptions in the JSON output must be in **{game.Language}**. Write all JSON content in {game.Language}.");

        var threadList = string.Join("\n", highMomentumThreads.Select(t =>
            $"- [{t.Category}] {t.Title} (momentum: {t.Momentum:F1})\n  {t.Description}\n  Current milestone: {t.NextMilestone ?? "None"}"));

        var userPrompt =
            $"Threads reaching critical momentum:\n{threadList}\n\n" +
            "For each thread, generate a specific milestone event. Make it concrete and actionable.";

        try
        {
            var result = await provider.CompleteAsync(systemPrompt, userPrompt, new LLMOptions
            {
                Temperature = 0.7f,
                MaxTokens = 2048
            });

            var json = result.Trim();
            if (json.StartsWith("```"))
            {
                var lines = json.Split('\n');
                json = string.Join('\n', lines.Skip(1).Take(lines.Length - 2));
            }

            var milestones = JsonSerializer.Deserialize<List<MilestoneTemplate>>(json);
            if (milestones == null || !milestones.Any())
                return new List<MilestoneEvent>();

            var spawned = new List<MilestoneEvent>();
            foreach (var milestone in milestones)
            {
                var thread = highMomentumThreads.FirstOrDefault(t => t.Id.ToString() == milestone.threadId);
                if (thread == null)
                    continue;

                var milestoneEvent = new MilestoneEvent
                {
                    Title = milestone.milestoneTitle,
                    Description = milestone.milestoneDescription,
                    Status = MilestoneStatus.Pending,
                    CreatedAt = DateTime.UtcNow
                };

                thread.MilestoneEvents.Add(milestoneEvent);
                // Mark as the current milestone
                thread.NextMilestone = $"[PENDING] {milestone.milestoneTitle}";
                thread.UpdatedAt = DateTime.UtcNow;

                spawned.Add(milestoneEvent);
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Spawned {Count} milestone events for game {GameId}", spawned.Count, gameId);

            return spawned;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to spawn milestones for game {GameId}", gameId);
            return new List<MilestoneEvent>();
        }
    }

    public async Task<List<StoryOpportunity>> DetectOpportunitiesAsync(Guid gameId, string context)
    {
        var game = await _context.Games
            .Include(g => g.LLMPreset)
            .FirstOrDefaultAsync(g => g.Id == gameId);

        if (game == null)
            throw new KeyNotFoundException($"Game {gameId} not found.");

        if (game.LLMPreset == null)
            throw new InvalidOperationException("Cannot detect opportunities: no LLM preset configured.");

        var provider = GetProvider(game.LLMPreset);
        if (provider == null)
            throw new InvalidOperationException($"LLM provider '{game.LLMPreset.ProviderType}' not available.");

        var threads = await _context.PlotThreads
            .Where(t => t.GameId == gameId && t.Status == PlotThreadStatus.Active)
            .ToListAsync();

        var systemPrompt =
        """
You are a TTRPG plot architect analyzing the current game state for story opportunities.
Based on recent events, identify what should happen next. Look for:
1. New plot threads that have emerged
2. Existing threads that need milestone events (high momentum = urgent)
3. Threads that should be adapted (low momentum = fading)
4. Threat threads that need escalation

Respond with ONLY a JSON array in this exact format (no markdown, no extra text):
[
  {{
    "type": "NewThread",
    "title": "...",
    "description": "...",
    "newThreadCategory": "Faction",
    "newThreadTitle": "...",
    "newThreadDescription": "..."
  }},
  {{
    "type": "SpawnMilestone",
    "title": "...",
    "description": "...",
    "threadId": "uuid",
    "newMilestone": "..."
  }},
  ...
]

Only include genuine opportunities — not every minor event needs a response.
""" +
            (string.IsNullOrEmpty(game.Language) || game.Language == "English" ? "" :
                $"\n\n**Language**: All thread titles, descriptions, and milestone text in the JSON output must be in **{game.Language}**. Write all JSON content in {game.Language}.");

        var threadSummary = string.Join("\n", threads.Take(10).Select(t =>
            $"- [{t.Category}] {t.Title} (momentum: {t.Momentum:F1}, relevance: {t.RelevanceScore:F2})\n  {t.Description}"));

        var userPrompt =
            $"Active plot threads:\n{threadSummary}\n\n" +
            $"Recent game events:\n{context}\n\n" +
            "What story opportunities have emerged? Be selective — only flag genuine opportunities.";

        try
        {
            var result = await provider.CompleteAsync(systemPrompt, userPrompt, new LLMOptions
            {
                Temperature = 0.75f,
                MaxTokens = 3072
            });

            var json = result.Trim();
            if (json.StartsWith("```"))
            {
                var lines = json.Split('\n');
                json = string.Join('\n', lines.Skip(1).Take(lines.Length - 2));
            }

            var opportunities = JsonSerializer.Deserialize<List<StoryOpportunityTemplate>>(json);
            if (opportunities == null || !opportunities.Any())
                return new List<StoryOpportunity>();

            // Validate and return opportunities
            return opportunities.Where(o => Enum.TryParse(o.type, true, out OpportunityType _)).Select(o => new StoryOpportunity
            {
                Type = Enum.Parse<OpportunityType>(o.type, true),
                Title = o.title,
                Description = o.description,
                ThreadId = o.threadId,
                MomentumDelta = o.momentumDelta,
                NewThreadCategory = o.newThreadCategory,
                NewThreadTitle = o.newThreadTitle,
                NewThreadDescription = o.newThreadDescription,
                NewMilestone = o.newMilestone
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to detect opportunities for game {GameId}", gameId);
            return new List<StoryOpportunity>();
        }
    }

    private static PlotThreadCategory ParseCategory(string category)
    {
        return category.ToLowerInvariant() switch
        {
            "faction" => PlotThreadCategory.Faction,
            "mystery" => PlotThreadCategory.Mystery,
            "personal" => PlotThreadCategory.Personal,
            "threat" => PlotThreadCategory.Threat,
            "worldevent" => PlotThreadCategory.WorldEvent,
            "relationship" => PlotThreadCategory.Relationship,
            _ => PlotThreadCategory.General
        };
    }

    private static PlotThreadStatus ParseStatus(string status)
    {
        return status.ToLowerInvariant() switch
        {
            "active" => PlotThreadStatus.Active,
            "resolved" => PlotThreadStatus.Resolved,
            "abandoned" => PlotThreadStatus.Abandoned,
            _ => PlotThreadStatus.Active
        };
    }
}

// DTOs for LLM communication
internal class ThreadTemplate
{
    public string title { get; set; } = string.Empty;
    public string category { get; set; } = string.Empty;
    public string description { get; set; } = string.Empty;
    public string nextMilestone { get; set; } = string.Empty;
    public string foreshadowing { get; set; } = string.Empty;
}

internal class ThreadAdaptation
{
    public string threadId { get; set; } = string.Empty;
    public float momentum { get; set; }
    public string status { get; set; } = string.Empty;
    public string description { get; set; } = string.Empty;
    public string nextMilestone { get; set; } = string.Empty;
    public float relevance { get; set; }
    public string reason { get; set; } = string.Empty;
}

internal class MilestoneTemplate
{
    public string threadId { get; set; } = string.Empty;
    public string milestoneTitle { get; set; } = string.Empty;
    public string milestoneDescription { get; set; } = string.Empty;
}

internal class StoryOpportunityTemplate
{
    public string type { get; set; } = string.Empty;
    public string title { get; set; } = string.Empty;
    public string description { get; set; } = string.Empty;
    public string? threadId { get; set; }
    public float? momentumDelta { get; set; }
    public string? newThreadCategory { get; set; }
    public string? newThreadTitle { get; set; }
    public string? newThreadDescription { get; set; }
    public string? newMilestone { get; set; }
}
