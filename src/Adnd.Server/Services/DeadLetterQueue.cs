using Adnd.Server.Data;
using Adnd.Server.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Adnd.Server.Services;

/// <summary>
/// Dead Letter Queue for failed agent calls that exceed retry limits.
/// Failed calls are moved here for manual inspection/retry rather than being lost.
/// </summary>
public interface IDeadLetterQueue
{
    /// <summary>
    /// Add a failed call to the dead letter queue.
    /// </summary>
    Task EnqueueAsync(AgentCall failedCall, string reason);

    /// <summary>
    /// Get pending dead letter entries for inspection.
    /// </summary>
    Task<List<AgentCall>> GetPendingAsync(int limit = 50);

    /// <summary>
    /// Retry a dead letter entry by re-queuing it as pending.
    /// </summary>
    Task RetryAsync(Guid callId);

    /// <summary>
    /// Archive a dead letter entry (mark as permanently discarded).
    /// </summary>
    Task ArchiveAsync(Guid callId, string reason);

    /// <summary>
    /// Get dead letter statistics for a game.
    /// </summary>
    Task<(int Pending, int Archived)> GetStatsAsync(Guid gameId);
}

public class DeadLetterQueue : IDeadLetterQueue
{
    private readonly AppDbContext _context;
    private readonly ILogger<DeadLetterQueue> _logger;

    public DeadLetterQueue(AppDbContext context, ILogger<DeadLetterQueue> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task EnqueueAsync(AgentCall failedCall, string reason)
    {
        // Increment retry count
        var retryCount = failedCall.Metadata?.GetProperty("DeadLetterRetryCount").GetInt32() ?? 0;
        
        if (retryCount >= 3)
        {
            // Max retries exceeded — archive permanently
            await ArchiveAsync(failedCall.Id, $"Max retries ({retryCount}) exceeded: {reason}");
            return;
        }

        // Store in a dedicated DLQ table (AgentCalls with status 'DeadLetter')
        failedCall.Status = AgentCallStatus.Failed;
        failedCall.Error = $"DLQ: {reason} (retry {retryCount + 1}/3)";
        
        // Update retry count in metadata
        if (failedCall.Metadata == null || failedCall.Metadata.Value.ValueKind == System.Text.Json.JsonValueKind.Undefined)
        {
            failedCall.Metadata = System.Text.Json.JsonSerializer.SerializeToElement(new { DeadLetterRetryCount = retryCount + 1 });
        }
        else
        {
            var dict = failedCall.Metadata.Value.GetDict();
            dict["DeadLetterRetryCount"] = retryCount + 1;
            failedCall.Metadata = System.Text.Json.JsonSerializer.SerializeToElement(dict);
        }

        await _context.SaveChangesAsync();

        _logger.LogWarning("Agent call {CallId} moved to DLQ: {Reason}", failedCall.Id, reason);
    }

    public async Task<List<AgentCall>> GetPendingAsync(int limit = 50)
    {
        return await _context.AgentCalls
            .Where(c => c.Status == AgentCallStatus.Failed && c.Error != null && c.Error.StartsWith("DLQ:"))
            .OrderByDescending(c => c.CreatedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task RetryAsync(Guid callId)
    {
        var call = await _context.AgentCalls.FindAsync(callId);
        if (call == null) throw new KeyNotFoundException($"DLQ entry {callId} not found.");
        if (call.Error == null || !call.Error.StartsWith("DLQ:"))
            throw new InvalidOperationException("Call is not in the dead letter queue.");

        call.Status = AgentCallStatus.Pending;
        call.Error = null;
        call.StartedAt = null;
        call.CompletedAt = null;
        call.DurationMs = 0;

        await _context.SaveChangesAsync();
        _logger.LogInformation("DLQ entry {CallId} re-queued as pending", callId);
    }

    public async Task ArchiveAsync(Guid callId, string reason)
    {
        var call = await _context.AgentCalls.FindAsync(callId);
        if (call == null) throw new KeyNotFoundException($"DLQ entry {callId} not found.");

        call.Status = AgentCallStatus.Failed;
        call.Error = $"Archived: {reason}";
        call.CompletedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        _logger.LogInformation("DLQ entry {CallId} archived: {Reason}", callId, reason);
    }

    public async Task<(int Pending, int Archived)> GetStatsAsync(Guid gameId)
    {
        var pending = await _context.AgentCalls
            .CountAsync(c => c.GameId == gameId && c.Status == AgentCallStatus.Failed && 
                             c.Error != null && c.Error.StartsWith("DLQ:"));

        var archived = await _context.AgentCalls
            .CountAsync(c => c.GameId == gameId && c.Status == AgentCallStatus.Failed && 
                             c.Error != null && c.Error.StartsWith("Archived:"));

        return (pending, archived);
    }
}

// Helper extension for JsonElement to Dictionary
internal static class JsonElementExtensions
{
    public static Dictionary<string, int> GetDict(this System.Text.Json.JsonElement element)
    {
        if (element.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            return element.EnumerateObject().ToDictionary(
                p => p.Name, 
                p => p.Value.ValueKind == System.Text.Json.JsonValueKind.Number ? p.Value.GetInt32() : 0);
        }
        return new();
    }
}
