using Adnd.Server.Data;
using Adnd.Server.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Adnd.Server.Services;

/// <summary>
/// Background service that performs periodic maintenance tasks:
/// - Cleans up expired GM tool calls (waiting confirmation past timeout)
/// - Archives old plot threads (resolved/abandoned for >30 days)
/// </summary>
public class MaintenanceService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MaintenanceService> _logger;
    private Timer? _timer;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(15);
    private readonly TimeSpan _archiveInterval = TimeSpan.FromHours(24);
    private DateTime _lastCleanup = DateTime.MinValue;
    private DateTime _lastArchive = DateTime.MinValue;

    public MaintenanceService(
        IServiceScopeFactory scopeFactory,
        ILogger<MaintenanceService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Maintenance service starting");
        _timer = new Timer(DoWork, null, TimeSpan.Zero, _cleanupInterval);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Change(Timeout.Infinite, 0);
        _timer?.Dispose();
        _logger.LogInformation("Maintenance service stopped");
        return Task.CompletedTask;
    }

    private async void DoWork(object? state)
    {
        try
        {
            var now = DateTime.UtcNow;

            // Cleanup expired tool calls every 15 minutes
            if ((now - _lastCleanup) >= _cleanupInterval)
            {
                await CleanupExpiredToolCallsAsync();
                _lastCleanup = now;
            }

            // Archive old plot threads every 24 hours
            if ((now - _lastArchive) >= _archiveInterval)
            {
                await ArchiveOldPlotThreadsAsync();
                _lastArchive = now;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in maintenance service");
        }
    }

    /// <summary>
    /// Clean up expired GM tool calls that are still waiting for confirmation.
    /// Tool calls expire 5 minutes after creation by default.
    /// </summary>
    private async Task CleanupExpiredToolCallsAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var expiredCalls = await context.GMToolCalls
            .Where(tc => tc.Status == ToolCallStatus.WaitingConfirmation &&
                         tc.CreatedAt.Add(tc.ExpirationTime ?? TimeSpan.FromMinutes(5)) < DateTime.UtcNow)
            .ToListAsync();

        if (!expiredCalls.Any())
        {
            _logger.LogDebug("No expired tool calls to clean up");
            return;
        }

        foreach (var call in expiredCalls)
        {
            call.Status = ToolCallStatus.Cancelled;
            call.Error = "Expired: waiting for confirmation timed out";
            call.CompletedAt = DateTime.UtcNow;
        }

        await context.SaveChangesAsync();
        _logger.LogInformation("Cleaned up {Count} expired tool calls", expiredCalls.Count);
    }

    /// <summary>
    /// Archive old plot threads that have been resolved/abandoned for more than 30 days.
    /// Uses soft-delete (IsDeleted) to preserve data for audit purposes.
    /// </summary>
    private async Task ArchiveOldPlotThreadsAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var threshold = DateTime.UtcNow.AddDays(-30);

        var oldThreads = await context.PlotThreads
            .Where(t => (t.Status == PlotThreadStatus.Resolved || t.Status == PlotThreadStatus.Abandoned) &&
                        t.UpdatedAt.HasValue &&
                        t.UpdatedAt.Value < threshold)
            .ToListAsync();

        if (!oldThreads.Any())
        {
            _logger.LogDebug("No old plot threads to archive");
            return;
        }

        foreach (var thread in oldThreads)
        {
            thread.IsDeleted = true;
            thread.DeletedAt = DateTime.UtcNow;
        }

        await context.SaveChangesAsync();
        _logger.LogInformation("Archived {Count} old plot threads", oldThreads.Count);
    }
}
