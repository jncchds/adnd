using System.Text;
using System.Text.Json;
using Adnd.Server.Data;
using Adnd.Server.Models;
using Adnd.Server.Services.Llm;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Adnd.Server.Services;

/// <summary>
/// What the creation wizard prefills after the player presses "Suggest the rest". Every field
/// is a suggestion the player can still overwrite, so nothing here is authoritative — but
/// <see cref="Race"/> and <see cref="Background"/> are constrained to the catalogues the wizard
/// actually offers, because a value outside them would silently render as a blank dropdown.
/// </summary>
public record CharacterConcept(
    string Name,
    string Backstory,
    string? Race,
    string Class,
    string? Background,
    Dictionary<string, int> Attributes);

public interface ICharacterConceptService
{
    /// <summary>
    /// Suggests the parts of a character the player has not filled in yet, grounded in the
    /// game's premise and what has actually happened at the table. A non-empty
    /// <paramref name="name"/> or <paramref name="backstory"/> is treated as fixed and comes
    /// back unchanged; with both empty this is the "surprise me" path and invents them too.
    /// </summary>
    Task<CharacterConcept> SuggestAsync(Guid gameId, string? name, string? backstory, CancellationToken ct = default);
}

public class CharacterConceptService(
    AppDbContext db,
    ICharacterCreationFactory creation,
    IRAGService rag,
    INarrativeGenerationFactory narrative,
    ILogger<CharacterConceptService> logger) : ICharacterConceptService
{
    private static readonly string[] Abilities = ["STR", "DEX", "CON", "INT", "WIS", "CHA"];

    /// <summary>The wizard's own array — a suggestion that failed to match it would look like a bug.</summary>
    private static readonly int[] StandardArray = [15, 14, 13, 12, 10, 8];

    public async Task<CharacterConcept> SuggestAsync(Guid gameId, string? name, string? backstory, CancellationToken ct = default)
    {
        var game = await db.Games.FirstOrDefaultAsync(g => g.Id == gameId, ct)
            ?? throw new KeyNotFoundException("Game not found.");

        name = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        backstory = string.IsNullOrWhiteSpace(backstory) ? null : backstory.Trim();

        var races = creation.GetAvailableRaces();
        var backgrounds = creation.GetAvailableBackgrounds();

        var systemPrompt = BuildSystemPrompt(game, races, backgrounds);
        var userPrompt = await BuildUserPromptAsync(gameId, game, name, backstory, ct);

        var response = await narrative.GenerateStructuredAsync(gameId, systemPrompt, userPrompt, 0.9f, ct);

        if (string.IsNullOrWhiteSpace(response))
            throw new InvalidOperationException("The game has no LLM preset configured, so a character cannot be suggested.");

        if (!JsonExtract.TryExtractObject(response, out var obj))
        {
            logger.LogWarning("Character suggestion for game {GameId} was not JSON: {Response}", gameId, Truncate(response, 400));
            throw new InvalidOperationException("The model did not return a usable character. Try again.");
        }

        return new CharacterConcept(
            Name: name ?? Clean(obj, "name", 100) ?? "Unnamed Wanderer",
            Backstory: backstory ?? Clean(obj, "backstory", 4000) ?? string.Empty,
            Race: MatchRace(Clean(obj, "race", 60), races),
            Class: Clean(obj, "class", 60) ?? "Fighter",
            Background: MatchBackground(Clean(obj, "background", 60), backgrounds),
            Attributes: ReadAttributes(obj));
    }

    private static string BuildSystemPrompt(Game game, IReadOnlyList<string> races, List<BackgroundDefinition> backgrounds)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You build player characters for a tabletop RPG. You are given a campaign premise, what has happened at the table so far, and whatever the player has already written down. You fill in the rest so the character belongs in *this* campaign — not a generic one.");
        sb.AppendLine();
        sb.AppendLine($"System: {game.SystemId}.");
        sb.AppendLine();
        sb.AppendLine("Respond with a single JSON object and nothing else. No prose, no code fence, no commentary.");
        sb.AppendLine("""
            {
              "name": "the character's name",
              "backstory": "2-4 paragraphs: origin, motivation, personality, and a concrete hook tying them to the campaign",
              "race": "one of the races listed below, exactly as spelled",
              "class": "a class appropriate to the system, e.g. Fighter, Wizard, Rogue, Cleric",
              "background": "one of the background ids listed below, exactly as spelled",
              "attributes": { "STR": 0, "DEX": 0, "CON": 0, "INT": 0, "WIS": 0, "CHA": 0 }
            }
            """);
        sb.AppendLine();
        sb.AppendLine($"Races: {string.Join(", ", races)}");
        sb.AppendLine("Background ids (pick the one whose description fits the backstory):");
        foreach (var bg in backgrounds)
            sb.AppendLine($"  {bg.Id} — {bg.Name}: {bg.Description}");
        sb.AppendLine();
        sb.AppendLine($"Attributes must use each of {string.Join(", ", StandardArray)} exactly once — assign the high scores to what the class and the backstory actually need.");

        // The directive Game.LanguageDirective carries is about narration; applied verbatim it
        // would also translate the race and background ids, which are matched against a
        // catalogue and would then match nothing.
        if (!string.IsNullOrWhiteSpace(game.Language) &&
            !game.Language.Equals("English", StringComparison.OrdinalIgnoreCase))
        {
            sb.AppendLine();
            sb.AppendLine($"Write \"name\" and \"backstory\" in {game.Language}. Keep the JSON keys and the \"race\", \"class\" and \"background\" values in English, exactly as listed above.");
        }

        return sb.ToString();
    }

    private async Task<string> BuildUserPromptAsync(Guid gameId, Game game, string? name, string? backstory, CancellationToken ct)
    {
        var sb = new StringBuilder();

        sb.AppendLine("=== CAMPAIGN ===");
        sb.AppendLine($"Title: {game.Name}");
        if (!string.IsNullOrWhiteSpace(game.PlotSeed))
            sb.AppendLine($"Premise: {game.PlotSeed}");
        if (!string.IsNullOrWhiteSpace(game.GameParameters))
            sb.AppendLine($"Parameters: {game.GameParameters}");
        sb.AppendLine();

        // The same context the narrator gets: recent table messages, the NPCs in play and the
        // active plot threads. A character invented against this arrives already entangled.
        var context = await rag.GeneratePlotContextAsync(gameId, ct);
        if (!string.IsNullOrWhiteSpace(context))
        {
            sb.AppendLine("=== WHAT HAS HAPPENED SO FAR ===");
            sb.AppendLine(context);
        }

        var party = await db.Characters
            .Where(c => c.Player.GameId == gameId)
            .Select(c => new { c.Name, c.Race, c.Class })
            .ToListAsync(ct);

        if (party.Count > 0)
        {
            sb.AppendLine("=== THE REST OF THE PARTY ===");
            foreach (var member in party)
                sb.AppendLine($"{member.Name} — {member.Race ?? "unknown race"} {member.Class}");
            sb.AppendLine("Do not duplicate an existing party member's niche.");
            sb.AppendLine();
        }

        sb.AppendLine("=== WHAT THE PLAYER HAS WRITTEN ===");
        sb.AppendLine(name is null ? "Name: (none — invent one)" : $"Name: {name}");
        sb.AppendLine(backstory is null
            ? "Backstory: (none — invent one that fits the campaign above)"
            : $"Backstory:\n{backstory}");
        sb.AppendLine();
        sb.AppendLine(name is null && backstory is null
            ? "Invent the whole character from scratch, fitting the campaign."
            : "Derive the remaining fields from what the player wrote — do not contradict it. Repeat the fields they supplied back unchanged.");

        return sb.ToString();
    }

    private static string? Clean(JsonElement obj, string property, int maxLength)
    {
        if (!obj.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.String)
            return null;
        var text = value.GetString()?.Trim();
        if (string.IsNullOrEmpty(text)) return null;
        return text.Length > maxLength ? text[..maxLength] : text;
    }

    private static string? MatchRace(string? suggested, IReadOnlyList<string> races)
    {
        if (suggested is null) return null;
        // Anything outside the catalogue is dropped rather than passed through: the wizard's
        // race field is a dropdown, and an unknown value renders as an empty one.
        return races.FirstOrDefault(r => r.Equals(suggested, StringComparison.OrdinalIgnoreCase))
            ?? races.FirstOrDefault(r => suggested.Contains(r, StringComparison.OrdinalIgnoreCase));
    }

    private static string? MatchBackground(string? suggested, List<BackgroundDefinition> backgrounds)
    {
        if (suggested is null) return null;
        var normalized = suggested.Replace(" ", "").Replace("-", "");
        return backgrounds.FirstOrDefault(b => b.Id.Equals(normalized, StringComparison.OrdinalIgnoreCase))?.Id
            ?? backgrounds.FirstOrDefault(b => b.Name.Equals(suggested, StringComparison.OrdinalIgnoreCase))?.Id;
    }

    /// <summary>
    /// Reads the six scores, then forces them onto the standard array by rank. Models routinely
    /// return point-buy totals, a 17, or five values — and the wizard warns about anything that
    /// isn't the standard array, so an unconstrained suggestion would arrive pre-flagged as
    /// suspect. Ranking preserves the model's actual intent (which ability matters most) while
    /// guaranteeing a legal spread.
    /// </summary>
    private static Dictionary<string, int> ReadAttributes(JsonElement obj)
    {
        var raw = new Dictionary<string, int>();
        if (obj.TryGetProperty("attributes", out var attrs) && attrs.ValueKind == JsonValueKind.Object)
        {
            // Matched by prefix rather than by exact key: models write "str", "Strength" and
            // "STR" interchangeably, and TryGetProperty is case-sensitive.
            foreach (var prop in attrs.EnumerateObject())
            {
                if (prop.Value.ValueKind != JsonValueKind.Number || !prop.Value.TryGetInt32(out var score))
                    continue;
                var ability = Abilities.FirstOrDefault(a => prop.Name.StartsWith(a, StringComparison.OrdinalIgnoreCase));
                if (ability is not null)
                    raw.TryAdd(ability, score);
            }
        }

        var ordered = Abilities
            .OrderByDescending(a => raw.GetValueOrDefault(a, 0))
            .ThenBy(a => Array.IndexOf(Abilities, a))
            .ToList();

        return ordered
            .Select((ability, rank) => (ability, score: StandardArray[rank]))
            .ToDictionary(x => x.ability, x => x.score);
    }

    private static string Truncate(string text, int max) => text.Length <= max ? text : text[..max];
}
