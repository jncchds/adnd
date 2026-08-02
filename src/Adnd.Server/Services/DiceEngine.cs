using System.Text.RegularExpressions;

namespace Adnd.Server.Services;

public record DiceResult(
    string Formula,
    int Total,
    List<int> IndividualRolls,
    List<int> KeptRolls,
    string Breakdown,
    /// <summary>
    /// The face of the first kept d20, or null if the formula rolled no d20. "Natural 1"
    /// is a property of that die, not of Total, which the modifier has already moved —
    /// a Halfling with +3 who rolls a 1 totals 4, and nothing in Total says so.
    /// </summary>
    int? NaturalD20 = null);

public interface IDiceEngine
{
    DiceResult Roll(string formula);
}

public partial class DiceEngine : IDiceEngine
{
    [GeneratedRegex(@"[+-]\d+d\d+(?:(?:k[hl]|d[hl])\d+)?|[+-]\d+", RegexOptions.IgnoreCase)]
    private static partial Regex TokenPattern();

    [GeneratedRegex(@"(\d+)d(\d+)((?:k[hl]|d[hl])\d+)?", RegexOptions.IgnoreCase)]
    private static partial Regex DicePartPattern();

    private const int MaxDiceCount = 1000;
    private const int MaxDiceSides = 1000;
    private const int MaxFormulaLength = 200;

    public DiceResult Roll(string formula)
    {
        if (string.IsNullOrWhiteSpace(formula))
            throw new ArgumentException("A dice formula is required.");
        if (formula.Length > MaxFormulaLength)
            throw new ArgumentException($"Dice formula must be {MaxFormulaLength} characters or fewer.");

        var normalized = formula.Replace(" ", "").ToLower();
        if (!normalized.StartsWith('-') && !normalized.StartsWith('+'))
            normalized = "+" + normalized;

        var allIndividualRolls = new List<int>();
        var allKeptRolls = new List<int>();
        var breakdownParts = new List<string>();
        var total = 0;
        int? naturalD20 = null;

        // TokenPattern only matches well-formed dice/number terms, so it silently skips
        // over anything else — a formula like "1d20+{strength}" (an LLM emitting an
        // unresolved ability-modifier placeholder instead of a number) used to have that
        // whole term vanish with no error, quietly under-totaling the roll. Requiring every
        // matched token to butt up against the last one means any unrecognized fragment
        // fails loudly instead of being dropped.
        var consumed = 0;
        foreach (Match match in TokenPattern().Matches(normalized))
        {
            if (match.Index != consumed)
            {
                var badFragment = normalized[consumed..match.Index];
                throw new ArgumentException(
                    $"Dice formula \"{formula}\" contains \"{badFragment}\", which isn't a number or dice term. " +
                    "Use a fully-resolved formula like \"1d20+3\" — no placeholders.");
            }
            consumed = match.Index + match.Length;

            var token = match.Value;
            var sign = token[0] == '-' ? -1 : 1;
            var body = token[1..];

            if (body.Contains('d'))
            {
                var dm = DicePartPattern().Match(body);

                // Bounded before allocation. These come straight from a user-supplied
                // formula, so "999999999d20" used to allocate ~4 GB, and a longer digit
                // string threw OverflowException out of int.Parse.
                if (!int.TryParse(dm.Groups[1].Value, out var count) || count is < 1 or > MaxDiceCount)
                    throw new ArgumentException($"Dice count must be between 1 and {MaxDiceCount}.");
                if (!int.TryParse(dm.Groups[2].Value, out var sides) || sides is < 2 or > MaxDiceSides)
                    throw new ArgumentException($"Dice sides must be between 2 and {MaxDiceSides}.");

                var modStr = dm.Groups[3].Value;

                var rolls = new int[count];
                for (var i = 0; i < count; i++)
                    rolls[i] = Random.Shared.Next(1, sides + 1);

                var keptIndices = GetKeptIndices(rolls, modStr);
                var groupSum = keptIndices.Sum(i => rolls[i]);

                total += groupSum * sign;
                allIndividualRolls.AddRange(rolls);
                var kept = keptIndices.OrderBy(i => i).Select(i => rolls[i]).ToList();
                allKeptRolls.AddRange(kept);

                // First kept d20 wins: with advantage ("2d20kh1") exactly one is kept, and
                // that is the die the table would call the natural roll.
                if (sides == 20 && naturalD20 is null && kept.Count > 0)
                    naturalD20 = kept[0];

                var rollDisplay = string.Join(",", Enumerable.Range(0, count)
                    .Select(i => keptIndices.Contains(i) ? rolls[i].ToString() : $"~~{rolls[i]}~~"));
                var prefix = sign == 1 ? "+" : "-";
                breakdownParts.Add($"{prefix}[{rollDisplay}]d{sides}={groupSum}");
            }
            else
            {
                var absValue = int.Parse(body);
                total += absValue * sign;
                var prefix = sign == 1 ? "+" : "-";
                breakdownParts.Add($"{prefix}{absValue}");
            }
        }

        if (consumed != normalized.Length)
        {
            var badFragment = normalized[consumed..];
            throw new ArgumentException(
                $"Dice formula \"{formula}\" contains \"{badFragment}\", which isn't a number or dice term. " +
                "Use a fully-resolved formula like \"1d20+3\" — no placeholders.");
        }

        // A single term's own text already states the result ("+[14]d20=14") — appending
        // "= 14" again just repeated the same number and was the part players found most
        // confusing. Only show the running total once there's an actual sum of 2+ terms.
        var breakdown = breakdownParts.Count > 1
            ? string.Join(" ", breakdownParts) + $" = {total}"
            : string.Join(" ", breakdownParts);
        return new DiceResult(formula, total, allIndividualRolls, allKeptRolls, breakdown, naturalD20);
    }

    private static HashSet<int> GetKeptIndices(int[] rolls, string modStr)
    {
        if (string.IsNullOrEmpty(modStr))
            return Enumerable.Range(0, rolls.Length).ToHashSet();

        var modType = modStr[..2];
        var modN = int.Parse(modStr[2..]);

        return modType switch
        {
            "kh" => Enumerable.Range(0, rolls.Length)
                .OrderByDescending(i => rolls[i]).Take(modN).ToHashSet(),
            "kl" => Enumerable.Range(0, rolls.Length)
                .OrderBy(i => rolls[i]).Take(modN).ToHashSet(),
            "dh" => Enumerable.Range(0, rolls.Length)
                .OrderBy(i => rolls[i]).Take(rolls.Length - modN).ToHashSet(),
            "dl" => Enumerable.Range(0, rolls.Length)
                .OrderByDescending(i => rolls[i]).Take(rolls.Length - modN).ToHashSet(),
            _ => Enumerable.Range(0, rolls.Length).ToHashSet()
        };
    }
}
