using Microsoft.EntityFrameworkCore;
using Adnd.Server.Data;
using Adnd.Server.Models;

namespace Adnd.Server.Services;

/// <summary>
/// Service for dice roll statistics — aggregates roll data across sessions.
/// </summary>
public interface IDiceStatsService
{
    Task<DiceStatsResponse> GetStatsAsync(Guid gameId, Guid? sessionId = null);
    Task<PlayerDiceStatsResponse> GetPlayerStatsAsync(Guid gameId, Guid playerId, Guid? sessionId = null);
}

public class DiceStatsService : IDiceStatsService
{
    private readonly AppDbContext _context;

    public DiceStatsService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<DiceStatsResponse> GetStatsAsync(Guid gameId, Guid? sessionId = null)
    {
        var messages = _context.Messages
            .Where(m => m.Session.GameId == gameId && m.Type == MessageType.Dice)
            .AsQueryable();

        if (sessionId.HasValue)
            messages = messages.Where(m => m.SessionId == sessionId.Value);

        var entries = await messages
            .Select(m => m.Metadata)
            .Where(m => m.ValueKind == System.Text.Json.JsonValueKind.Object)
            .ToListAsync();

        // Parse dice results from metadata
        var allRolls = new List<int>();
        var diceTypes = new Dictionary<int, int>();
        var totalSum = 0;
        var totalCount = 0;

        foreach (var meta in entries)
        {
            if (meta.TryGetProperty("total", out var totalProp) && totalProp.ValueKind == System.Text.Json.JsonValueKind.Number)
            {
                var total = totalProp.GetInt32();
                allRolls.Add(total);
                totalSum += total;
                totalCount++;
            }
            if (meta.TryGetProperty("diceType", out var dtProp) && dtProp.ValueKind == System.Text.Json.JsonValueKind.Number)
            {
                var dt = dtProp.GetInt32();
                diceTypes[dt] = diceTypes.GetValueOrDefault(dt) + 1;
            }
            if (meta.TryGetProperty("rolls", out var rollsProp) && rollsProp.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var roll in rollsProp.EnumerateArray())
                {
                    if (roll.ValueKind == System.Text.Json.JsonValueKind.Number)
                    {
                        var r = roll.GetInt32();
                        allRolls.Add(r);
                    }
                }
            }
        }

        var avg = totalCount > 0 ? (double)totalSum / totalCount : 0;
        var min = allRolls.Count > 0 ? allRolls.Min() : 0;
        var max = allRolls.Count > 0 ? allRolls.Max() : 0;
        var median = allRolls.Count > 0 ? ComputeMedian(allRolls) : 0;
        var distribution = ComputeDistribution(allRolls);

        return new DiceStatsResponse
        {
            GameId = gameId,
            SessionId = sessionId,
            TotalRolls = totalCount,
            AverageRoll = Math.Round(avg, 2),
            MinRoll = min,
            MaxRoll = max,
            MedianRoll = median,
            DiceTypes = diceTypes,
            Distribution = distribution
        };
    }

    public async Task<PlayerDiceStatsResponse> GetPlayerStatsAsync(Guid gameId, Guid playerId, Guid? sessionId = null)
    {
        var messages = _context.Messages
            .Where(m => m.Session.GameId == gameId && m.Type == MessageType.Dice && m.PlayerId == playerId)
            .AsQueryable();

        if (sessionId.HasValue)
            messages = messages.Where(m => m.SessionId == sessionId.Value);

        var entries = await messages
            .Select(m => new { m.Metadata, m.CreatedAt, m.Player })
            .ToListAsync();

        var allRolls = new List<int>();
        var diceTypes = new Dictionary<int, int>();
        var totalSum = 0;
        var totalCount = 0;
        var successCount = 0;
        var failureCount = 0;

        foreach (var entry in entries)
        {
            var meta = entry.Metadata;
            if (meta.ValueKind != System.Text.Json.JsonValueKind.Object) continue;

            if (meta.TryGetProperty("total", out var totalProp) && totalProp.ValueKind == System.Text.Json.JsonValueKind.Number)
            {
                var total = totalProp.GetInt32();
                allRolls.Add(total);
                totalSum += total;
                totalCount++;

                // Count successes (natural 20) and failures (natural 1)
                if (meta.TryGetProperty("rolls", out var rollsProp) && rollsProp.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (var roll in rollsProp.EnumerateArray())
                    {
                        if (roll.ValueKind == System.Text.Json.JsonValueKind.Number)
                        {
                            var r = roll.GetInt32();
                            if (r == 20) successCount++;
                            if (r == 1) failureCount++;
                        }
                    }
                }
            }
            if (meta.TryGetProperty("diceType", out var dtProp) && dtProp.ValueKind == System.Text.Json.JsonValueKind.Number)
            {
                var dt = dtProp.GetInt32();
                diceTypes[dt] = diceTypes.GetValueOrDefault(dt) + 1;
            }
        }

        var avg = totalCount > 0 ? (double)totalSum / totalCount : 0;
        var min = allRolls.Count > 0 ? allRolls.Min() : 0;
        var max = allRolls.Count > 0 ? allRolls.Max() : 0;
        var median = allRolls.Count > 0 ? ComputeMedian(allRolls) : 0;
        var distribution = ComputeDistribution(allRolls);

        return new PlayerDiceStatsResponse
        {
            GameId = gameId,
            SessionId = sessionId,
            PlayerId = playerId,
            PlayerName = entries.FirstOrDefault()?.Player?.CharacterName ?? "Unknown",
            TotalRolls = totalCount,
            AverageRoll = Math.Round(avg, 2),
            MinRoll = min,
            MaxRoll = max,
            MedianRoll = median,
            NaturalTwenties = successCount,
            NaturalOnes = failureCount,
            DiceTypes = diceTypes,
            Distribution = distribution
        };
    }

    private static double ComputeMedian(List<int> values)
    {
        if (values.Count == 0) return 0;
        var sorted = values.OrderBy(v => v).ToList();
        var mid = sorted.Count / 2;
        return sorted.Count % 2 == 0 ? (sorted[mid - 1] + sorted[mid]) / 2.0 : sorted[mid];
    }

    private static Dictionary<int, int> ComputeDistribution(List<int> values)
    {
        var dist = new Dictionary<int, int>();
        foreach (var v in values)
        {
            dist[v] = dist.GetValueOrDefault(v) + 1;
        }
        return dist.OrderBy(d => d.Key).ToDictionary(d => d.Key, d => d.Value);
    }
}

public class DiceStatsResponse
{
    public Guid GameId { get; set; }
    public Guid? SessionId { get; set; }
    public int TotalRolls { get; set; }
    public double AverageRoll { get; set; }
    public int MinRoll { get; set; }
    public int MaxRoll { get; set; }
    public double MedianRoll { get; set; }
    public Dictionary<int, int> DiceTypes { get; set; } = new();
    public Dictionary<int, int> Distribution { get; set; } = new();
}

public class PlayerDiceStatsResponse
{
    public Guid GameId { get; set; }
    public Guid? SessionId { get; set; }
    public Guid PlayerId { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public int TotalRolls { get; set; }
    public double AverageRoll { get; set; }
    public int MinRoll { get; set; }
    public int MaxRoll { get; set; }
    public double MedianRoll { get; set; }
    public int NaturalTwenties { get; set; }
    public int NaturalOnes { get; set; }
    public Dictionary<int, int> DiceTypes { get; set; } = new();
    public Dictionary<int, int> Distribution { get; set; } = new();
}
