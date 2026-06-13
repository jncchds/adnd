using Adnd.Server.Data;
using Adnd.Server.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Adnd.Server.Services;

/// <summary>
/// Service for managing GM tool call lifecycle: validation, expiration cleanup, and history.
/// </summary>
public interface IGMToolCallService
{
    /// <summary>
    /// Validate a tool call before execution.
    /// Returns null if valid, or an error message if invalid.
    /// </summary>
    string? ValidateToolCall(string toolName, string? arguments);

    /// <summary>
    /// Clean up expired tool calls that are waiting for confirmation.
    /// Returns the number of expired calls cleaned up.
    /// </summary>
    Task<int> CleanupExpiredCallsAsync(Guid gameId);

    /// <summary>
    /// Get the full history of tool calls for a game.
    /// </summary>
    Task<List<GMToolCall>> GetToolCallHistoryAsync(Guid gameId, int limit = 50);

    /// <summary>
    /// Get pending tool calls that are waiting for confirmation.
    /// </summary>
    Task<List<GMToolCall>> GetPendingConfirmationsAsync(Guid gameId);
}

public class GMToolCallService : IGMToolCallService
{
    private readonly AppDbContext _context;
    private readonly ILogger<GMToolCallService> _logger;

    public GMToolCallService(AppDbContext context, ILogger<GMToolCallService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public string? ValidateToolCall(string toolName, string? arguments)
    {
        // Validate tool name
        if (string.IsNullOrWhiteSpace(toolName))
            return "Tool name is required.";

        // Known tool names validation
        var knownTools = new[]
        {
            "roll_dice", "skill_check", "attack", "spell_cast", "condition",
            "npc_action", "environment", "narrate", "query", "state",
            "player_roll", "combat_start", "combat_end", "initiative"
        };

        if (!knownTools.Contains(toolName.ToLower()))
        {
            _logger.LogWarning("Unknown tool call requested: {ToolName}", toolName);
        }

        // Validate JSON arguments if provided
        if (!string.IsNullOrWhiteSpace(arguments))
        {
            try
            {
                System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(arguments);
            }
            catch (Exception ex)
            {
                return $"Invalid JSON in tool arguments: {ex.Message}";
            }
        }

        // Validate argument length
        if (!string.IsNullOrWhiteSpace(arguments) && arguments.Length > 10000)
            return "Tool arguments too long (max 10000 characters).";

        return null;
    }

    public async Task<int> CleanupExpiredCallsAsync(Guid gameId)
    {
        var expiredCalls = await _context.GMToolCalls
            .Where(tc => tc.GameId == gameId && 
                         tc.RequiresConfirmation && 
                         tc.Status == ToolCallStatus.WaitingConfirmation &&
                         tc.CreatedAt.Add(tc.ExpirationTime) < DateTime.UtcNow)
            .ToListAsync();

        if (!expiredCalls.Any())
            return 0;

        foreach (var call in expiredCalls)
        {
            call.Status = ToolCallStatus.Cancelled;
            call.Error = "Expired: waiting for confirmation timed out";
            call.CompletedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Cleaned up {Count} expired tool calls for game {GameId}", expiredCalls.Count, gameId);
        return expiredCalls.Count;
    }

    public async Task<List<GMToolCall>> GetToolCallHistoryAsync(Guid gameId, int limit = 50)
    {
        return await _context.GMToolCalls
            .Where(tc => tc.GameId == gameId)
            .OrderByDescending(tc => tc.CreatedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<List<GMToolCall>> GetPendingConfirmationsAsync(Guid gameId)
    {
        return await _context.GMToolCalls
            .Where(tc => tc.GameId == gameId && 
                         tc.RequiresConfirmation && 
                         tc.Status == ToolCallStatus.WaitingConfirmation)
            .OrderByDescending(tc => tc.CreatedAt)
            .ToListAsync();
    }
}
