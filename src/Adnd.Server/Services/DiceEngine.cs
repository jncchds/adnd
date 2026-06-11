namespace Adnd.Server.Services;

/// <summary>
/// Parses and rolls dice with formulas like "2d6+3", "4d20kl1", "1d20-2".
/// Supports: d, k (keep lowest), h (keep highest), r (drop lowest/highest).
/// </summary>
public interface IDiceEngine
{
    DiceRollResult Roll(string formula, Guid? playerId = null);
    string FormatResult(DiceRollResult result);
}

public class DiceEngine : IDiceEngine
{
    // Random.Shared is thread-safe — avoids biased results from concurrent new Random() calls
    private readonly ILogger<DiceEngine> _logger;

    public DiceEngine(ILogger<DiceEngine> logger)
    {
        _logger = logger;
    }

    public DiceRollResult Roll(string formula, Guid? playerId = null)
    {
        if (string.IsNullOrWhiteSpace(formula))
            throw new ArgumentException("Dice formula cannot be empty.", nameof(formula));

        var (diceCount, diceType, modifier, options) = ParseFormula(formula);

        if (diceCount <= 0 || diceType <= 0)
            throw new ArgumentException($"Invalid dice formula: {formula}");

        var rolls = new int[diceCount];
        for (int i = 0; i < diceCount; i++)
        {
            rolls[i] = Random.Shared.Next(1, diceType + 1);
        }

        // Apply keep/drop options
        var finalRolls = ApplyOptions(rolls, options);
        var subtotal = finalRolls.Sum();
        var total = subtotal + modifier;

        var result = new DiceRollResult
        {
            Formula = formula.Trim(),
            DiceCount = diceCount,
            DiceType = diceType,
            Modifier = modifier,
            Rolls = rolls,
            FinalRolls = finalRolls,
            Subtotal = subtotal,
            Total = total,
            PlayerId = playerId,
            RolledAt = DateTime.UtcNow
        };

        _logger.LogDebug("Dice roll: {Formula} -> {Total} (rolls: [{Rolls}])",
            result.Formula, result.Total, string.Join(", ", result.FinalRolls));

        return result;
    }

    public string FormatResult(DiceRollResult result)
    {
        var parts = new List<string>
        {
            $"{result.Formula} = {result.Total}"
        };

        if (result.FinalRolls.Length > 0)
        {
            parts.Add($"({string.Join(" + ", result.FinalRolls)}" +
                (result.Modifier != 0 ? $" {result.Modifier:+#;-#;0})" : ")"));
        }
        else
        {
            parts.Add("(no rolls)");
        }

        if (result.Rolls.Length != result.FinalRolls.Length)
        {
            parts.Add($"[original: {string.Join(", ", result.Rolls)}]");
        }

        return string.Join("  ", parts);
    }

    private (int count, int type, int modifier, DiceOptions options) ParseFormula(string formula)
    {
        var trimmed = formula.Trim();
        var options = new DiceOptions();

        // Parse keep/drop modifiers (e.g., "kh1", "kl3", "dr2") — allow spaces between parts
        var kMatch = System.Text.RegularExpressions.Regex.Match(trimmed, @"(kh|kl|hr|hl|dr|dh)\s*(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (kMatch.Success)
        {
            var mode = kMatch.Groups[1].Value.ToLower();
            var count = int.Parse(kMatch.Groups[2].Value);

            switch (mode)
            {
                case "kh": options.KeepHighest = count; break;
                case "kl": options.KeepLowest = count; break;
                case "hr":
                case "hl": options.DropHighest = count; break;
                case "dr":
                case "dh": options.DropLowest = count; break;
            }
            trimmed = trimmed.Remove(kMatch.Index, kMatch.Length).Trim();
        }

        // Parse modifier at the end (e.g., "2d6+3", "1d20-2") — allow spaces
        var modMatch = System.Text.RegularExpressions.Regex.Match(trimmed, @"^\s*(\d+)\s*d\s*(\d+)\s*([+-]\d+)\s*$");
        if (modMatch.Success)
        {
            var count = int.Parse(modMatch.Groups[1].Value);
            var type = int.Parse(modMatch.Groups[2].Value);
            var modifier = int.Parse(modMatch.Groups[3].Value);
            return (count, type, modifier, options);
        }

        // Parse simple dice (e.g., "2d6") — allow spaces
        var simpleMatch = System.Text.RegularExpressions.Regex.Match(trimmed, @"^\s*(\d+)\s*d\s*(\d+)\s*$");
        if (simpleMatch.Success)
        {
            var count = int.Parse(simpleMatch.Groups[1].Value);
            var type = int.Parse(simpleMatch.Groups[2].Value);
            return (count, type, 0, options);
        }

        // Parse single number (e.g., "1" = auto-success/failure)
        if (int.TryParse(trimmed, out var num))
        {
            return (1, 1, num, options);
        }

        throw new ArgumentException($"Cannot parse dice formula: {formula}");
    }

    private int[] ApplyOptions(int[] rolls, DiceOptions options)
    {
        var result = rolls;

        // Validate mutual exclusivity: can't keep and drop in the same direction
        if (options.KeepHighest > 0 && options.KeepLowest > 0)
            throw new ArgumentException("Cannot use both KeepHighest and KeepLowest in the same roll.");
        if (options.DropHighest > 0 && options.DropLowest > 0)
            throw new ArgumentException("Cannot use both DropHighest and DropLowest in the same roll.");
        if ((options.KeepHighest > 0 || options.KeepLowest > 0) && (options.DropHighest > 0 || options.DropLowest > 0))
            throw new ArgumentException("Cannot mix keep and drop options in the same roll.");

        if (options.KeepHighest > 0)
        {
            result = result.OrderByDescending(r => r).Take(options.KeepHighest).ToArray();
        }

        if (options.KeepLowest > 0)
        {
            result = result.OrderBy(r => r).Take(options.KeepLowest).ToArray();
        }

        if (options.DropHighest > 0 && options.DropHighest < result.Length)
        {
            result = result.OrderBy(r => r).Take(result.Length - options.DropHighest).ToArray();
        }

        if (options.DropLowest > 0 && options.DropLowest < result.Length)
        {
            result = result.OrderByDescending(r => r).Take(result.Length - options.DropLowest).ToArray();
        }

        return result;
    }
}

public class DiceOptions
{
    public int KeepHighest { get; set; }
    public int KeepLowest { get; set; }
    public int DropHighest { get; set; }
    public int DropLowest { get; set; }
}

public class DiceRollResult
{
    public string Formula { get; set; } = string.Empty;
    public int DiceCount { get; set; }
    public int DiceType { get; set; }
    public int Modifier { get; set; }
    public int[] Rolls { get; set; } = Array.Empty<int>();
    public int[] FinalRolls { get; set; } = Array.Empty<int>();
    public int Subtotal { get; set; }
    public int Total { get; set; }
    public Guid? PlayerId { get; set; }
    public DateTime RolledAt { get; set; }
}
