using System.Text.Json;
using Adnd.Server.Data;
using Adnd.Server.Models;
using Adnd.Server.Services.Llm;
using Microsoft.EntityFrameworkCore;
using CombatEntity = Adnd.Server.Models.Combat;

namespace Adnd.Server.Services.Combat;

public sealed class CombatAIService(AppDbContext db, ILLMProviderFactory llmProviderFactory) : ICombatAIService
{
    public async Task<List<AICombatSuggestion>> GetSuggestionsAsync(
        Guid combatId,
        Guid gameId,
        CancellationToken ct = default)
    {
        // Load game + preset
        var game = await db.Games
            .Include(g => g.LLMPreset)
            .FirstOrDefaultAsync(g => g.Id == gameId, ct);

        if (game?.LLMPreset is null)
            return [];  // No preset configured — return empty

        // Load combat state
        var combat = await db.Combats
            .Include(c => c.Participants)
            .FirstOrDefaultAsync(c => c.Id == combatId, ct);

        if (combat is null) return [];

        var context = BuildCombatContext(combat);
        var provider = llmProviderFactory.CreateFromPreset(game.LLMPreset);

        var systemPrompt = """
            You are a D&D 5e combat assistant. Analyze the current combat state and provide
            tactical suggestions for each participant. Respond with a JSON array only.
            Format: [{"participantName":"...", "suggestion":"...", "reasoning":"..."}]
            """;

        try
        {
            var opts = new LLMOptions
            {
                Model = game.LLMPreset.BaseModel,
                Temperature = 0.7f,
                MaxTokens = 1024
            };

            var response = await provider.CompleteAsync(systemPrompt, context, opts, ct);

            if (JsonExtract.TryExtractArray(response, out var array))
            {
                var suggestions = new List<AICombatSuggestion>();
                foreach (var item in array.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object) continue;

                    var name = item.TryGetProperty("participantName", out var n) ? n.GetString() ?? string.Empty : string.Empty;
                    var suggestion = item.TryGetProperty("suggestion", out var s) ? s.GetString() ?? string.Empty : string.Empty;
                    var reasoning = item.TryGetProperty("reasoning", out var r) ? r.GetString() ?? string.Empty : string.Empty;

                    if (!string.IsNullOrEmpty(name))
                        suggestions.Add(new AICombatSuggestion(name, suggestion, reasoning));
                }

                return suggestions;
            }
        }
        catch
        {
            // LLM failure — return empty suggestions gracefully
        }

        return [];
    }

    private static string BuildCombatContext(CombatEntity combat)
    {
        var parts = combat.Participants
            .OrderByDescending(p => p.Initiative)
            .Select(p => $"- {p.DisplayName}: HP {p.HP}/{p.MaxHP}, AC {p.AC}, Initiative {p.Initiative}");

        return $"""
            Current combat round: {combat.CurrentRound}
            Current turn index: {combat.CurrentTurnIndex}

            Participants (in initiative order):
            {string.Join("\n", parts)}
            """;
    }
}
