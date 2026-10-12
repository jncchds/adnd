using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

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
public static partial class TurnLog
{
    public const string Tool = "tool";
    public const string Said = "said";
    public const string Directive = "directive";

    /// <summary>How a tool result starts when the engine refused the call. The rest of that
    /// reply's calls were chosen on the assumption it would happen, so they are not run.</summary>
    public const string Refused = "Not done — ";

    /// <summary>A directive after which the model is offered no tools: all that is left is to narrate.</summary>
    public const string Close = "close";

    public static List<TurnLogEntry> Read(JsonElement? stored)
        => stored is { ValueKind: JsonValueKind.Array } array
            ? JsonSerializer.Deserialize<List<TurnLogEntry>>(array.GetRawText()) ?? []
            : [];

    public static JsonElement Write(List<TurnLogEntry> entries) => JsonSerializer.SerializeToElement(entries);

    /// <summary>A model sent back for more often answers with the line it has just said.</summary>
    public static bool AlreadySaid(IEnumerable<TurnLogEntry> entries, string text)
        => Unsaid(entries, text).Length == 0;

    /// <summary>
    /// The part of <paramref name="text"/> the table has not already been told this turn.
    /// Only identical lines used to be dropped, but a model rarely repeats itself to the
    /// letter: it rewords the line, or opens its next reply by retelling the last one before
    /// adding to it. So a reply that says nothing new is dropped whole, and one that retells
    /// before it adds keeps only what it adds.
    /// </summary>
    public static string Unsaid(IEnumerable<TurnLogEntry> entries, string text)
    {
        text = text.Trim();
        var heard = entries.Where(e => e.Kind == Said || (e.Kind == Tool && e.Name == "narrate"))
            .Select(e => e.Result).ToList();
        if (text.Length == 0 || heard.Count == 0) return text;

        var words = Words(text);
        if (heard.Select(Words).Any(h => Retells(words, h)))
            return "";

        var heardSentences = heard.SelectMany(h => SentenceBreak().Split(h)).Select(Words).Where(w => w.Count >= 4).ToList();
        if (heardSentences.Count == 0) return text;

        // Split keeps the whitespace between sentences as its own piece, so what is kept is
        // put back together as it was written, paragraph breaks included.
        var pieces = SentenceBreak().Split(text);
        var kept = new StringBuilder();
        for (var i = 0; i < pieces.Length; i += 2)
        {
            var sentence = Words(pieces[i]);
            if (sentence.Count >= 4 && heardSentences.Any(h => Retells(sentence, h))) continue;
            kept.Append(pieces[i]);
            if (i + 1 < pieces.Length) kept.Append(pieces[i + 1]);
        }
        var rest = kept.ToString().Trim();
        return Words(rest).Count == 0 ? "" : rest;
    }

    [GeneratedRegex(@"(?<=[.!?…][""'”’»)*_]*)(\s+)")]
    private static partial Regex SentenceBreak();

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex Word();

    private static HashSet<string> Words(string text)
        => Word().Matches(text.ToLowerInvariant()).Select(m => m.Value).ToHashSet();

    private static int Overlap(HashSet<string> a, HashSet<string> b) => a.Count(b.Contains);

    /// <summary>Whether <paramref name="said"/> is <paramref name="heard"/> over again: much
    /// the same words, a part of it, or all of it with a clause added.</summary>
    private static bool Retells(HashSet<string> said, HashSet<string> heard)
    {
        if (said.Count == 0 || heard.Count == 0) return false;
        var shared = Overlap(said, heard);
        return shared >= 0.8 * (said.Count + heard.Count - shared)
            || (said.Count >= 5 && shared >= 0.9 * said.Count)
            || (heard.Count >= 5 && shared >= 0.9 * heard.Count && said.Count <= 1.75 * heard.Count);
    }

    /// <summary>Whether the last reply did nothing but tell the table something.</summary>
    public static bool LastRoundOnlySpoke(IReadOnlyList<TurnLogEntry> entries)
        => entries.Count > 0 && entries.Where(e => e.Round == entries[^1].Round)
            .All(e => e.Kind == Said || (e.Kind == Tool && e.Name == "narrate"));

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

        // A model that describes the blow and stops tends, asked again, to describe it again.
        if (toolsAllowed && LastRoundOnlySpoke(entries))
        {
            sb.AppendLine();
            sb.AppendLine("Your last reply only described what is being attempted. The table has read it; do not describe it again. Now make the tool call that resolves it.");
        }

        sb.AppendLine();
        sb.AppendLine(toolsAllowed
            ? "Continue from here. If the results above call for more — damage after a hit, a follow-up roll, the next combatant's turn — call those tools now, with a sentence of narration alongside. Do only what follows from what the player said: never give a player character a further action, attack or choice of your own. When nothing is left for you to resolve and the next move belongs to a player, reply with plain narration and no tool calls. Narrate only what you have not already told the table; if there is nothing to add, a single short line is enough."
            : "You have no tool calls left this turn. Reply with plain narration only: what happened that you have not already told the table, and what the players now face.");
        return sb.ToString();
    }
}
