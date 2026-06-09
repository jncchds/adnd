using System.Security.Cryptography;
using System.Text.Json;
using Adnd.Server.Data;
using Adnd.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Services;

/// <summary>
/// Handles reviewing and adapting plot threads based on recent game events.
/// </summary>
public class PlotThreadAdaptationStrategy
{
    private readonly ILLMProviderFactory _providerFactory;
    private readonly IApiKeyEncryptionService _encryption;

    public PlotThreadAdaptationStrategy(
        ILLMProviderFactory providerFactory,
        IApiKeyEncryptionService encryption)
    {
        _providerFactory = providerFactory;
        _encryption = encryption;
    }

    public async Task<PlotReview> ReviewAndAdaptAsync(
        Guid gameId, string context, string trigger, LLMPreset preset,
        List<PlotThread> threads, AppDbContext db, string gameLanguage, ILogger logger)
    {
        var provider = GetProvider(preset);
        if (provider == null)
            throw new InvalidOperationException($"LLM provider '{preset.ProviderType}' not available.");

        var languageHint = string.IsNullOrEmpty(gameLanguage) || gameLanguage == "English" ? "" :
            $"\n\n**Language**: All plot thread descriptions, titles, and milestones in the JSON output must be in **{gameLanguage}**. Write all JSON content in {gameLanguage}.";

        var systemPrompt =
        """
You are a TTRPG plot architect reviewing the current state of a game's story.
The players have made choices that have shifted the narrative. Review each plot thread and decide:

1. **Momentum**: How urgent is this thread now? Scale from -10 (abandoned/fading) to +10 (urgent/critical).
2. **Status**: Keep Active, or mark as Resolved (if the thread's goal is achieved) or Abandoned (if no longer relevant).
3. **Description**: Update the description to reflect how events have changed this thread.
4. **Next Milestone**: Update the next expected event based on what has happened.
5. **Relevance**: How relevant is this thread to the current game state? 0-1 scale.

Respond with ONLY a JSON array in this exact format (no markdown, no extra text):
[
  {
    "threadId": "uuid",
    "momentum": 5,
    "status": "Active",
    "description": "Updated description",
    "nextMilestone": "Next expected event",
    "relevance": 0.8,
    "reason": "Why these changes"
  },
  ...
]

Only include threads that need changes. Threads not listed keep their current values.
"""
            + languageHint;

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

            var json = result.Trim();
            if (json.StartsWith("```"))
            {
                var lines = json.Split('\n');
                json = string.Join('\n', lines.Skip(1).Take(lines.Length - 2));
            }

            var updates = JsonSerializer.Deserialize<List<ThreadAdaptation>>(json);
            if (updates == null || !updates.Any())
            {
                logger.LogWarning("PlotWeaver review returned no adaptations for game {GameId}", gameId);
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

            await db.SaveChangesAsync();

            logger.LogInformation("PlotWeaver review for game {GameId}: {Summary}", gameId, review.Summary);

            return review;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to review plot threads for game {GameId}", gameId);
            return new PlotReview
            {
                GameId = gameId,
                Trigger = trigger,
                Summary = $"Review failed: {ex.Message}"
            };
        }
    }

    private ILLMProvider? GetProvider(LLMPreset preset)
    {
        if (preset.ApiKey != null && preset.DecryptedApiKey == null)
        {
            try
            {
                preset.DecryptedApiKey = _encryption.Decrypt(preset.ApiKey);
            }
            catch (CryptographicException)
            {
                return null;
            }
        }

        return _providerFactory.CreateFromPreset(preset);
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

/// <summary>
/// DTO for LLM communication — thread adaptation.
/// </summary>
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
