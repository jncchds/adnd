using System.Text.Json;
using Adnd.Server.Data;
using Adnd.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Services;

/// <summary>
/// PlotWeaver — automatically generates, maintains, and evolves plot threads
/// based on game events. Fully automatic: no manual triggers needed.
///
/// Orchestrates four strategy classes:
/// - PlotThreadGenerationStrategy (initial + dynamic thread generation)
/// - PlotThreadAdaptationStrategy (review and adapt existing threads)
/// - PlotMilestoneSpawningStrategy (spawn milestone events)
/// - PlotOpportunityDetectionStrategy (detect story opportunities)
/// </summary>
public class PlotWeaver : IPlotWeaver
{
    private readonly AppDbContext _context;
    private readonly ILLMProviderFactory _providerFactory;
    private readonly IApiKeyEncryptionService _encryption;
    private readonly IRAGService _ragService;
    private readonly IEmbeddingService _embeddingService;
    private readonly ILogger<PlotWeaver> _logger;
    private readonly PlotThreadGenerationStrategy _threadGenerationStrategy;
    private readonly PlotThreadAdaptationStrategy _threadAdaptationStrategy;
    private readonly PlotMilestoneSpawningStrategy _milestoneSpawningStrategy;
    private readonly PlotOpportunityDetectionStrategy _opportunityDetectionStrategy;

    public PlotWeaver(
        AppDbContext context,
        ILLMProviderFactory providerFactory,
        IApiKeyEncryptionService encryption,
        IRAGService ragService,
        IEmbeddingService embeddingService,
        ILogger<PlotWeaver> logger)
    {
        _context = context;
        _providerFactory = providerFactory;
        _encryption = encryption;
        _ragService = ragService;
        _embeddingService = embeddingService;
        _logger = logger;

        _threadGenerationStrategy = new PlotThreadGenerationStrategy(providerFactory, encryption);
        _threadAdaptationStrategy = new PlotThreadAdaptationStrategy(providerFactory, encryption);
        _milestoneSpawningStrategy = new PlotMilestoneSpawningStrategy(providerFactory, encryption);
        _opportunityDetectionStrategy = new PlotOpportunityDetectionStrategy(providerFactory, encryption);
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

        var threads = await _threadGenerationStrategy.GenerateInitialAsync(
            gameId, premise, parameters, systemId, game.LLMPreset, _embeddingService, game.Language, _logger);

        if (!threads.Any())
        {
            threads = await GenerateFallbackThreads(gameId, premise, parameters, systemId);
        }

        foreach (var thread in threads)
        {
            _context.PlotThreads.Add(thread);
        }
        await _context.SaveChangesAsync();

        return threads;
    }

    /// <summary>
    /// Fallback plot thread generation when the LLM fails.
    /// </summary>
    private async Task<List<PlotThread>> GenerateFallbackThreads(Guid gameId, string premise, string parameters, string systemId)
    {
        var fallbackThreads = new List<PlotThread>
        {
            new() { GameId = gameId, Title = "The Call to Adventure", Category = PlotThreadCategory.Personal, Description = $"A mysterious invitation arrives, drawing the players into the heart of the {premise}.", NextMilestone = "The first clue appears", Foreshadowing = "A symbol or phrase that recurs throughout the story", Momentum = 0f, RelevanceScore = 0.5f, Status = PlotThreadStatus.Active },
            new() { GameId = gameId, Title = "The Hidden Threat", Category = PlotThreadCategory.Threat, Description = "An unseen force grows stronger in the shadows, unaware of the players' approach.", NextMilestone = "First sign of the threat", Foreshadowing = "Strange occurrences that hint at the danger", Momentum = 0f, RelevanceScore = 0.5f, Status = PlotThreadStatus.Active },
            new() { GameId = gameId, Title = "Allies and Enemies", Category = PlotThreadCategory.Faction, Description = "Factions vie for control, some offering help, others seeking to exploit the players.", NextMilestone = "First faction contact", Foreshadowing = "Conflicting rumors about the factions", Momentum = 0f, RelevanceScore = 0.5f, Status = PlotThreadStatus.Active }
        };

        _context.PlotThreads.AddRange(fallbackThreads);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Generated {Count} fallback plot threads for game {GameId}", fallbackThreads.Count, gameId);
        return fallbackThreads;
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

        var threads = await _context.PlotThreads
            .Where(t => t.GameId == gameId && t.Status == PlotThreadStatus.Active)
            .ToListAsync();

        if (!threads.Any())
        {
            _logger.LogWarning("No active plot threads to review for game {GameId}", gameId);
            return new PlotReview { GameId = gameId, Trigger = trigger, Summary = "No active threads." };
        }

        return await _threadAdaptationStrategy.ReviewAndAdaptAsync(
            gameId, context, trigger, game.LLMPreset, threads, _context, game.Language, _logger);
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

    private async Task ComputeRelevanceScores(Guid gameId, List<PlotThread> threads)
    {
        var now = DateTime.UtcNow;
        foreach (var thread in threads)
        {
            if (thread.Status == PlotThreadStatus.Active)
            {
                var momentumWeight = (thread.Momentum + 10f) / 20f;
                var recency = thread.UpdatedAt.HasValue
                    ? (float)Math.Max(0, 1 - (now - thread.UpdatedAt.Value).TotalHours / 72)
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

        var existingThreads = await _context.PlotThreads
            .Where(t => t.GameId == gameId && t.Status == PlotThreadStatus.Active)
            .ToListAsync();

        var threads = await _threadGenerationStrategy.GenerateDynamicAsync(
            gameId, context, trigger, game.LLMPreset, _embeddingService, game.Language, _logger);

        if (threads.Any())
        {
            foreach (var thread in threads)
            {
                _context.PlotThreads.Add(thread);
            }
            await _context.SaveChangesAsync();
        }

        return threads ?? new List<PlotThread>();
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

        return await _milestoneSpawningStrategy.SpawnMilestonesAsync(
            gameId, game.LLMPreset, highMomentumThreads, _context, game.Language, _logger);
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

        var threads = await _context.PlotThreads
            .Where(t => t.GameId == gameId && t.Status == PlotThreadStatus.Active)
            .ToListAsync();

        return await _opportunityDetectionStrategy.DetectAsync(
            gameId, context, game.LLMPreset, threads, game.Language, _logger);
    }
}
