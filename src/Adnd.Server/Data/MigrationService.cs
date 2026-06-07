using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Adnd.Server.Data;

public class MigrationService
{
    private readonly AppDbContext _context;
    private readonly ILogger<MigrationService> _logger;

    public MigrationService(AppDbContext context, ILogger<MigrationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    private readonly object _migrationLock = new();

    public void ApplyMigrations()
    {
        // Prevent concurrent migration attempts
        lock (_migrationLock)
        {
            try
            {
                _context.Database.Migrate();
                _logger.LogInformation("Migrations applied successfully.");
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is DbException)
            {
                var msg = ex.Message;
                if (msg.Contains("does not exist") || msg.Contains("connection") || msg.Contains("Unable to connect"))
                {
                    _logger.LogWarning(ex, "Database not ready. Retrying with EnsureCreated...");
                    Thread.Sleep(2000);
                    try
                    {
                        _context.Database.EnsureCreated();
                        _logger.LogInformation("Database created via EnsureCreated.");
                    }
                    catch (Exception ensureEx)
                    {
                        _logger.LogError(ensureEx, "Failed to create database.");
                        throw;
                    }
                }
                else if (msg.Contains("already exists") || msg.Contains("already exist"))
                {
                    _logger.LogWarning("Tables already exist. Database is ready.");
                }
                else
                {
                    _logger.LogError(ex, "Migration failed: {Message}", msg);
                    throw;
                }
            }
        }
    }
}

public static class MigrationServiceExtensions
{
    public static WebApplication UseDatabaseMigrations(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var migrationService = scope.ServiceProvider.GetRequiredService<MigrationService>();
        migrationService.ApplyMigrations();
        return app;
    }
}
