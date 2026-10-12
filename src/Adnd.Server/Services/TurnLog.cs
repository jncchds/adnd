using System.Text;
using System.Text.Json;

namespace Adnd.Server.Services;

/// <summary>One thing the GM did during a turn: a tool it ran, something it told the table,
/// or a note the server gave it. <paramref name="Round"/> is the LLM response it belongs to.</summary>
public record TurnLogEntry(int Round, string Kind, string Name, string? Arguments, string Result);

/// <summary>
/// What the GM has done so far in the turn it is still resolving, kept on
/// <c>ToolCallCoordinator.ToolResults</c>. The model is stateless between calls, so this is
/// replayed to it on every step — which is also what lets a retried step resume instead of
/// running the turn's tools a second time.
/// </summary>
public static class TurnLog
{
    public const string Tool = "tool";
    public const string Said = "said";
    public const string Directive = "directive";

    /// <summary>A directive after which the model is offered no tools: all that is left is to narrate.</summary>
    public const string Close = "close";

    public static List<TurnLogEntry> Read(JsonElement? stored)
        => stored is { ValueKind: JsonValueKind.Array } array
            ? JsonSerializer.Deserialize<List<TurnLogEntry>>(array.GetRawText()) ?? []
            : [];

    public static JsonElement Write(List<TurnLogEntry> entries) => JsonSerializer.SerializeToElement(entries);

    /// <summary>A model sent back for more often answers with the line it has just said.</summary>
    public static bool AlreadySaid(IEnumerable<TurnLogEntry> entries, string text)
        => entries.Any(e => (e.Kind == Said || e.Name == "narrate")
            && string.Equals(e.Result.Trim(), text.Trim(), StringComparison.OrdinalIgnoreCase));

    public static bool HasSpoken(IEnumerable<TurnLogEntry> entries)
        => entries.Any(e => e.Kind == Said || (e.Kind == Tool && e.Name == "narrate"));

    /// <summary>The user prompt for every step after the first.</summary>
    public static string Continue(string originalPrompt, IReadOnlyList<TurnLogEntry> entries, string? combatState, bool toolsAllowed)
    {
        var sb = new StringBuilder(originalPrompt);
        sb.AppendLine().AppendLine();
        sb.AppendLine("=== YOUR TURN SO FAR ===");
        sb.AppendLine("You are partway through resolving the action above. This is what you have already done, in order. All of it has happened and the table has already seen it — never repeat it.");
        for (var i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            sb.Append(i + 1).Append(". ");
            sb.AppendLine(e.Kind switch
            {
                Said => $"You told the table: {e.Result}",
                Directive or Close => $"[Note from the game engine] {e.Result}",
                _ when e.Name == "narrate" => $"You told the table: {e.Result}",
                _ => $"{e.Name} {e.Arguments} → {e.Result}"
            });
        }

        if (combatState is not null)
        {
            sb.AppendLine();
            sb.AppendLine("=== THE FIGHT RIGHT NOW (this replaces any earlier description of the combat) ===");
            sb.AppendLine(combatState);
        }

        sb.AppendLine();
        sb.AppendLine(toolsAllowed
            ? "Continue from here. If the results above call for more — damage after a hit, a follow-up roll, the next combatant's turn — call those tools now, with a sentence of narration alongside. Do only what follows from what the player said: never give a player character a further action, attack or choice of your own. When nothing is left for you to resolve and the next move belongs to a player, reply with plain narration and no tool calls. Narrate only what you have not already told the table; if there is nothing to add, a single short line is enough."
            : "You have no tool calls left this turn. Reply with plain narration only: what happened that you have not already told the table, and what the players now face.");
        return sb.ToString();
    }
}
