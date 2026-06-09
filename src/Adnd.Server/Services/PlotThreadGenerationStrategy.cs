using System.Text.Json;
using Adnd.Server.Data;
using Adnd.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Services;

/// <summary>
/// Handles generating initial plot threads from game creation inputs.
/// Called once when a game starts.
/// </summary>
public class PlotThreadGenerationStrategy
{
    private readonly ILLMProviderRegistry _llmRegistry;
    private const string SystemPrompt =
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
        "]";

    public PlotThreadGenerationStrategy(ILLMProviderRegistry llmRegistry)
    {
        _llmRegistry = llmRegistry;
    }

    public async Task<List<PlotThread>> GenerateInitialAsync(
        Guid gameId, string premise, string parameters, string systemId,
        LLMPreset preset, IEmbeddingService embeddingService, string gameLanguage, ILogger logger)
    {
        var provider = GetProvider(preset);
        if (provider == null)
            throw new InvalidOperationException($"LLM provider '{preset.ProviderType}' not available.");

        var languageHint = string.IsNullOrEmpty(gameLanguage) || gameLanguage == "English" ? "" :
            $"\n\n**Language**: All plot thread titles, descriptions, and milestones must be in **{gameLanguage}**. Write all JSON content in {gameLanguage}.";

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
            var result = await provider.CompleteAsync(SystemPrompt + languageHint, userPrompt, new LLMOptions
            {
                Temperature = 0.8f,
                MaxTokens = 4096
            });

            var json = ExtractJson(result);
            var threads = JsonSerializer.Deserialize<List<ThreadTemplate>>(json);
            if (threads == null || !threads.Any())
                throw new InvalidOperationException("LLM returned no plot threads.");

            var embeddings = await embeddingService.GenerateEmbeddingsAsync(gameId, threads.Select(t => t.title + " " + t.description));

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
                plotThreads.Add(plotThread);
            }

            logger.LogInformation("Generated {Count} initial plot threads for game {GameId}", plotThreads.Count, gameId);
            return plotThreads;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate initial plot threads for game {GameId}", gameId);
            return new List<PlotThread>();
        }
    }

    public async Task<List<PlotThread>> GenerateDynamicAsync(
        Guid gameId, string context, string trigger, LLMPreset preset, IEmbeddingService embeddingService, string gameLanguage, ILogger logger)
    {
        var provider = GetProvider(preset);
        if (provider == null)
            throw new InvalidOperationException($"LLM provider '{preset.ProviderType}' not available.");

        var languageHint = string.IsNullOrEmpty(gameLanguage) || gameLanguage == "English" ? "" :
            $"\n\n**Language**: All plot thread titles, descriptions, and milestones must be in **{gameLanguage}**. Write all JSON content in {gameLanguage}.";

        var systemPrompt =
        """
You are a TTRPG plot architect. New events in the game have created story opportunities.
Generate NEW plot threads that emerge from these events. Do NOT repeat existing threads.

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
""" + languageHint;

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

            var json = ExtractJson(result);
            var threads = JsonSerializer.Deserialize<List<ThreadTemplate>>(json);
            if (threads == null || !threads.Any())
                return new List<PlotThread>();

            var embeddings = await embeddingService.GenerateEmbeddingsAsync(gameId, threads.Select(t => t.title + " " + t.description));

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
                    Momentum = 1f,
                    RelevanceScore = 0.6f,
                    Status = PlotThreadStatus.Active,
                    IsDynamic = true,
                    Embedding = embedding
                };
                plotThreads.Add(plotThread);
            }

            logger.LogInformation("Generated {Count} new dynamic plot threads for game {GameId} (trigger: {Trigger})",
                plotThreads.Count, gameId, trigger);
            return plotThreads;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate new plot threads for game {GameId}", gameId);
            return new List<PlotThread>();
        }
    }

    private ILLMProvider? GetProvider(LLMPreset preset)
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

    private static string ExtractJson(string result)
    {
        var json = result.Trim();
        if (json.StartsWith("```"))
        {
            var lines = json.Split('\n');
            json = string.Join('\n', lines.Skip(1).Take(lines.Length - 2));
        }
        var reasoningEnd = json.LastIndexOf("</reasoning>");
        if (reasoningEnd >= 0)
        {
            json = json.Substring(reasoningEnd + "</reasoning>".Length).Trim();
        }
        if (!json.StartsWith("[") && !json.StartsWith("{"))
        {
            var bracketIdx = json.IndexOfAny(new[] { '[', '{' });
            if (bracketIdx > 0)
            {
                json = json.Substring(bracketIdx);
            }
        }
        return json;
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
}

/// <summary>
/// DTO for LLM communication — thread generation.
/// </summary>
internal class ThreadTemplate
{
    public string title { get; set; } = string.Empty;
    public string category { get; set; } = string.Empty;
    public string description { get; set; } = string.Empty;
    public string nextMilestone { get; set; } = string.Empty;
    public string foreshadowing { get; set; } = string.Empty;
}
