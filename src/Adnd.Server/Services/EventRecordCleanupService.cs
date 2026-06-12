using Adnd.Server.Data;
using Adnd.Server.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Adnd.Server.Services;

/// <summary>
/// Periodic cleanup of acknowledged EventRecords older than 7 days.
/// Keeps the EventRecords table bounded in size.
/// </summary>
public class EventRecordCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EventRecordCleanupService> _logger;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(6);
    private readonly int _retentionDays = 7;

    public EventRecordCleanupService(
        IServiceScopeFactory scopeFactory,
        ILogger<EventRecordCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupAcknowledgedEvents(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in EventRecordCleanupService");
            }

            await Task.Delay(_cleanupInterval, stoppingToken);
        }
    }

    private async Task CleanupAcknowledgedEvents(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var cutoff = DateTime.UtcNow.AddDays(-_retentionDays);
        var deleted = await context.EventRecords
            .Where(e => e.Status == EventStatus.Acknowledged && e.AckedAt < cutoff)
            .ExecuteDeleteAsync(ct);

        if (deleted > 0)
        {
            _logger.LogInformation("Cleaned up {Count} acknowledged EventRecords older than {Days} days",
                deleted, _retentionDays);
        }
    }
}
