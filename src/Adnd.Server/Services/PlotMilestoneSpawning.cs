using System.Security.Cryptography;
using System.Text.Json;
using Adnd.Server.Data;
using Adnd.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Services;

/// <summary>
/// Handles spawning milestone events for high-momentum plot threads.
/// </summary>
public class PlotMilestoneSpawningStrategy
{
    private readonly ILLMProviderRegistry _llmRegistry;
    private readonly ILLMProviderFactory _providerFactory;
    private readonly IApiKeyEncryptionService _encryption;

    public PlotMilestoneSpawningStrategy(
        ILLMProviderRegistry llmRegistry,
        ILLMProviderFactory providerFactory,
        IApiKeyEncryptionService encryption)
    {
        _llmRegistry = llmRegistry;
        _providerFactory = providerFactory;
        _encryption = encryption;
    }

    public async Task<List<MilestoneEvent>> SpawnMilestonesAsync(
        Guid gameId, LLMPreset preset, List<PlotThread> highMomentumThreads,
        AppDbContext db, string gameLanguage, ILogger logger)
    {
        var provider = GetProvider(preset);
        if (provider == null)
            throw new InvalidOperationException($"LLM provider '{preset.ProviderType}' not available.");

        var languageHint = string.IsNullOrEmpty(gameLanguage) || gameLanguage == "English" ? "" :
            $"\n\n**Language**: All milestone titles and descriptions in the JSON output must be in **{gameLanguage}**. Write all JSON content in {gameLanguage}.";

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
"""
            + languageHint;

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
                thread.NextMilestone = $"[PENDING] {milestone.milestoneTitle}";
                thread.UpdatedAt = DateTime.UtcNow;

                spawned.Add(milestoneEvent);
            }

            await db.SaveChangesAsync();

            logger.LogInformation("Spawned {Count} milestone events for game {GameId}", spawned.Count, gameId);
            return spawned;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to spawn milestones for game {GameId}", gameId);
            return [];
        }
    }

    private ILLMProvider? GetProvider(LLMPreset preset)
    {
        var provider = _llmRegistry.GetProvider(preset.ProviderType);
        if (provider != null) return provider;

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
}

/// <summary>
/// DTO for LLM communication — milestone spawning.
/// </summary>
internal class MilestoneTemplate
{
    public string threadId { get; set; } = string.Empty;
    public string milestoneTitle { get; set; } = string.Empty;
    public string milestoneDescription { get; set; } = string.Empty;
}
