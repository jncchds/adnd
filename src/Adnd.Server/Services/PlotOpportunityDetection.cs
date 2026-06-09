using System.Security.Cryptography;
using System.Text.Json;
using Adnd.Server.Models;

namespace Adnd.Server.Services;

/// <summary>
/// Handles detecting story opportunities from recent game events.
/// Returns a list of suggested actions (new threads, milestones, adaptations).
/// </summary>
public class PlotOpportunityDetectionStrategy
{
    private readonly ILLMProviderRegistry _llmRegistry;
    private readonly ILLMProviderFactory _providerFactory;
    private readonly IApiKeyEncryptionService _encryption;

    public PlotOpportunityDetectionStrategy(
        ILLMProviderRegistry llmRegistry,
        ILLMProviderFactory providerFactory,
        IApiKeyEncryptionService encryption)
    {
        _llmRegistry = llmRegistry;
        _providerFactory = providerFactory;
        _encryption = encryption;
    }

    public async Task<List<StoryOpportunity>> DetectAsync(
        Guid gameId, string context, LLMPreset preset,
        List<PlotThread> threads, string gameLanguage, ILogger logger)
    {
        var provider = GetProvider(preset);
        if (provider == null)
            throw new InvalidOperationException($"LLM provider '{preset.ProviderType}' not available.");

        var languageHint = string.IsNullOrEmpty(gameLanguage) || gameLanguage == "English" ? "" :
            $"\n\n**Language**: All thread titles, descriptions, and milestone text in the JSON output must be in **{gameLanguage}**. Write all JSON content in {gameLanguage}.";

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
"""
            + languageHint;

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
            logger.LogError(ex, "Failed to detect opportunities for game {GameId}", gameId);
            return new List<StoryOpportunity>();
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
/// DTO for LLM communication — opportunity detection.
/// </summary>
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
