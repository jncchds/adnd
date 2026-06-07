using System.Linq;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Adnd.Server.Data;
using Adnd.Server.Models;

namespace Adnd.Server.Services;

public interface ILLMInteractionLogger
{
    /// <summary>
    /// Log a successful LLM interaction with token statistics.
    /// </summary>
    Task LogInteractionAsync(Guid userId, Guid? presetId, string providerType, string model,
        int? promptTokens, int? completionTokens, int? totalTokens,
        int durationMs, string systemPrompt, string userPrompt, string response,
        string? requestJson, string? responseJson,
        string origin, Guid? originGameId, Guid? originSessionId,
        string? originAgent, string? originAction);

    /// <summary>
    /// Log a failed LLM interaction with error details.
    /// </summary>
    Task LogFailureAsync(Guid userId, Guid? presetId, string providerType, string model,
        int? promptTokens, int? completionTokens, int? totalTokens,
        int durationMs, string error,
        string origin, Guid? originGameId, Guid? originSessionId,
        string? originAgent, string? originAction);

    /// <summary>
    /// Get interaction logs for a user, optionally filtered.
    /// </summary>
    Task<List<LLMInteractionLog>> GetLogsAsync(Guid userId, Guid? presetId = null,
        Guid? originGameId = null, string? providerType = null,
        DateTime? from = null, DateTime? to = null, int limit = 100);

    /// <summary>
    /// Get interaction logs for a specific game.
    /// </summary>
    Task<List<LLMInteractionLog>> GetGameLogsAsync(Guid gameId, int limit = 100);

    /// <summary>
    /// Get interaction logs grouped by preset with summary statistics.
    /// </summary>
    Task<List<PresetUsageSummary>> GetPresetUsageAsync(Guid userId, DateTime? from = null, DateTime? to = null);

    /// <summary>
    /// Get interaction logs grouped by provider+model for a specific game with summary statistics.
    /// Designed for game creators to monitor provider usage and plan spendings.
    /// </summary>
    Task<List<GameProviderUsageSummary>> GetGameProviderUsageAsync(Guid gameId, DateTime? from = null, DateTime? to = null);

    /// <summary>
    /// Delete old interaction logs (cleanup).
    /// </summary>
    Task DeleteOldLogsAsync(Guid userId, DateTime before);

    /// <summary>
    /// Delete a specific log entry.
    /// </summary>
    Task DeleteLogAsync(Guid userId, Guid logId);
}

public class LLMInteractionLogger : ILLMInteractionLogger
{
    private readonly AppDbContext _context;
    private readonly ILogger<LLMInteractionLogger> _logger;

    public LLMInteractionLogger(AppDbContext context, ILogger<LLMInteractionLogger> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task LogInteractionAsync(Guid userId, Guid? presetId, string providerType, string model,
        int? promptTokens, int? completionTokens, int? totalTokens,
        int durationMs, string systemPrompt, string userPrompt, string response,
        string? requestJson, string? responseJson,
        string origin, Guid? originGameId, Guid? originSessionId,
        string? originAgent, string? originAction)
    {
        var log = new LLMInteractionLog
        {
            UserId = userId,
            PresetId = presetId,
            ProviderType = providerType,
            Model = model,
            PromptTokens = promptTokens,
            CompletionTokens = completionTokens,
            TotalTokens = totalTokens,
            DurationMs = durationMs,
            Success = true,
            SystemPrompt = Truncate(systemPrompt, 4000),
            UserPrompt = Truncate(userPrompt, 4000),
            Response = Truncate(response, 4000),
            RequestJson = Truncate(requestJson, 8000),
            ResponseJson = Truncate(responseJson, 8000),
            Origin = origin,
            OriginGameId = originGameId,
            OriginSessionId = originSessionId,
            OriginAgent = originAgent,
            OriginAction = originAction,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        };

        _context.LLMInteractionLogs.Add(log);
        await _context.SaveChangesAsync();
    }

    public async Task LogFailureAsync(Guid userId, Guid? presetId, string providerType, string model,
        int? promptTokens, int? completionTokens, int? totalTokens,
        int durationMs, string error,
        string origin, Guid? originGameId, Guid? originSessionId,
        string? originAgent, string? originAction)
    {
        var log = new LLMInteractionLog
        {
            UserId = userId,
            PresetId = presetId,
            ProviderType = providerType,
            Model = model,
            PromptTokens = promptTokens,
            CompletionTokens = completionTokens,
            TotalTokens = totalTokens,
            DurationMs = durationMs,
            Success = false,
            Error = Truncate(error, 2000),
            Origin = origin,
            OriginGameId = originGameId,
            OriginSessionId = originSessionId,
            OriginAgent = originAgent,
            OriginAction = originAction,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        };

        _context.LLMInteractionLogs.Add(log);
        await _context.SaveChangesAsync();
    }

    public async Task<List<LLMInteractionLog>> GetLogsAsync(Guid userId, Guid? presetId = null,
        Guid? originGameId = null, string? providerType = null,
        DateTime? from = null, DateTime? to = null, int limit = 100)
    {
        IQueryable<LLMInteractionLog> query = _context.LLMInteractionLogs
            .Where(l => l.UserId == userId)
            .Include(l => l.Preset)
            .OrderByDescending(l => l.StartedAt);

        if (presetId.HasValue)
            query = query.Where(l => l.PresetId == presetId.Value);
        if (originGameId.HasValue)
            query = query.Where(l => l.OriginGameId == originGameId.Value);
        if (!string.IsNullOrEmpty(providerType))
            query = query.Where(l => l.ProviderType == providerType);
        if (from.HasValue)
            query = query.Where(l => l.StartedAt >= from.Value);
        if (to.HasValue)
            query = query.Where(l => l.StartedAt <= to.Value);

        return await query.Take(limit).ToListAsync();
    }

    public async Task<List<LLMInteractionLog>> GetGameLogsAsync(Guid gameId, int limit = 100)
    {
        return await _context.LLMInteractionLogs
            .Where(l => l.OriginGameId == gameId)
            .Include(l => l.Preset)
            .Include(l => l.User)
            .OrderByDescending(l => l.StartedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<List<PresetUsageSummary>> GetPresetUsageAsync(Guid userId, DateTime? from = null, DateTime? to = null)
    {
        var query = _context.LLMInteractionLogs
            .Where(l => l.UserId == userId && l.PresetId != null)
            .GroupBy(l => l.PresetId)
            .Select(g => new
            {
                PresetId = g.Key.Value,
                PresetName = g.First().Preset != null ? g.First().Preset.Name : "Deleted Preset",
                ProviderType = g.First().ProviderType,
                TotalCalls = g.Count(),
                SuccessfulCalls = g.Count(l => l.Success),
                FailedCalls = g.Count(l => !l.Success),
                TotalTokens = g.Sum(l => l.TotalTokens.GetValueOrDefault(0)),
                TotalPromptTokens = g.Sum(l => l.PromptTokens.GetValueOrDefault(0)),
                TotalCompletionTokens = g.Sum(l => l.CompletionTokens.GetValueOrDefault(0)),
                AvgDurationMs = g.Average(l => l.DurationMs),
                LastUsed = g.Max(l => l.StartedAt)
            });

        if (from.HasValue)
            query = query.Where(g => g.LastUsed >= from.Value);
        if (to.HasValue)
            query = query.Where(g => g.LastUsed <= to.Value);

        return await query.Select(g => new PresetUsageSummary
        {
            PresetId = g.PresetId,
            PresetName = g.PresetName,
            ProviderType = g.ProviderType,
            TotalCalls = g.TotalCalls,
            SuccessfulCalls = g.SuccessfulCalls,
            FailedCalls = g.FailedCalls,
            TotalTokens = g.TotalTokens,
            TotalPromptTokens = g.TotalPromptTokens,
            TotalCompletionTokens = g.TotalCompletionTokens,
            AvgDurationMs = g.AvgDurationMs,
            LastUsed = g.LastUsed
        }).ToListAsync();
    }

    public async Task DeleteOldLogsAsync(Guid userId, DateTime before)
    {
        var logs = await _context.LLMInteractionLogs
            .Where(l => l.UserId == userId && l.StartedAt < before)
            .ToListAsync();

        _context.LLMInteractionLogs.RemoveRange(logs);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteLogAsync(Guid userId, Guid logId)
    {
        var log = await _context.LLMInteractionLogs
            .FirstOrDefaultAsync(l => l.UserId == userId && l.Id == logId);

        if (log != null)
        {
            _context.LLMInteractionLogs.Remove(log);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<List<GameProviderUsageSummary>> GetGameProviderUsageAsync(Guid gameId, DateTime? from = null, DateTime? to = null)
    {
        var query = _context.LLMInteractionLogs
            .Where(l => l.OriginGameId == gameId)
            .GroupBy(l => new { l.ProviderType, l.Model })
            .Select(g => new
            {
                ProviderType = g.Key.ProviderType,
                Model = g.Key.Model,
                TotalCalls = g.Count(),
                SuccessfulCalls = g.Count(l => l.Success),
                FailedCalls = g.Count(l => !l.Success),
                SuccessRate = g.Count() > 0 ? (double)g.Count(l => l.Success) / g.Count() : 0.0,
                TotalTokens = g.Sum(l => l.TotalTokens.GetValueOrDefault(0)),
                TotalPromptTokens = g.Sum(l => l.PromptTokens.GetValueOrDefault(0)),
                TotalCompletionTokens = g.Sum(l => l.CompletionTokens.GetValueOrDefault(0)),
                AvgDurationMs = g.Average(l => l.DurationMs),
                MaxDurationMs = g.Max(l => l.DurationMs),
                FirstCall = g.Min(l => l.StartedAt),
                LastCall = g.Max(l => l.StartedAt)
            });

        if (from.HasValue)
            query = query.Where(g => g.FirstCall >= from.Value);
        if (to.HasValue)
            query = query.Where(g => g.LastCall <= to.Value);

        return await query.OrderByDescending(g => g.TotalCalls)
            .Select(g => new GameProviderUsageSummary
            {
                ProviderType = g.ProviderType,
                Model = g.Model,
                TotalCalls = g.TotalCalls,
                SuccessfulCalls = g.SuccessfulCalls,
                FailedCalls = g.FailedCalls,
                SuccessRate = g.SuccessRate,
                TotalTokens = g.TotalTokens,
                TotalPromptTokens = g.TotalPromptTokens,
                TotalCompletionTokens = g.TotalCompletionTokens,
                AvgDurationMs = g.AvgDurationMs,
                MaxDurationMs = g.MaxDurationMs,
                FirstCall = g.FirstCall,
                LastCall = g.LastCall
            }).ToListAsync();
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length > maxLength ? value[..maxLength] : value;
    }
}

public class PresetUsageSummary
{
    public Guid PresetId { get; set; }
    public string PresetName { get; set; } = string.Empty;
    public string ProviderType { get; set; } = string.Empty;
    public int TotalCalls { get; set; }
    public int SuccessfulCalls { get; set; }
    public int FailedCalls { get; set; }
    public int TotalTokens { get; set; }
    public int TotalPromptTokens { get; set; }
    public int TotalCompletionTokens { get; set; }
    public double AvgDurationMs { get; set; }
    public DateTime LastUsed { get; set; }
}

/// <summary>
/// Usage summary grouped by provider and model for a specific game.
/// Designed for game creators to monitor provider usage and plan spendings.
/// </summary>
public class GameProviderUsageSummary
{
    public string ProviderType { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int TotalCalls { get; set; }
    public int SuccessfulCalls { get; set; }
    public int FailedCalls { get; set; }
    public double SuccessRate { get; set; }
    public int TotalTokens { get; set; }
    public int TotalPromptTokens { get; set; }
    public int TotalCompletionTokens { get; set; }
    public double AvgDurationMs { get; set; }
    public int MaxDurationMs { get; set; }
    public DateTime FirstCall { get; set; }
    public DateTime LastCall { get; set; }
}
