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
            // Load pgvector extension if not already loaded.
            // This is required before any migration that uses vector columns runs.
            EnsurePgVectorExtension();

            var maxRetries = 5;
            var retryDelayMs = 2000;

            for (var attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    _context.Database.Migrate();
                    _logger.LogInformation("Migrations applied successfully.");
                    return;
                }
                catch (Exception ex) when (ex is InvalidOperationException || ex is DbException)
                {
                    var msg = ex.Message;

                    // Transient connection error — retry
                    if (msg.Contains("connection") || msg.Contains("Unable to connect") ||
                        msg.Contains("timeout") || msg.Contains("timed out"))
                    {
                        var delay = retryDelayMs * (attempt + 1);
                        _logger.LogWarning(ex, "Database not ready (attempt {Attempt}/{MaxRetries}). Retrying in {Delay}ms...",
                            attempt + 1, maxRetries, delay);
                        Thread.Sleep(delay);
                        continue;
                    }

                    // pgvector not loaded yet — create it and retry
                    if (msg.Contains("vector") && msg.Contains("does not exist"))
                    {
                        _logger.LogWarning("pgvector extension not loaded during migration. Loading now...");
                        try
                        {
                            _context.Database.ExecuteSqlRaw("CREATE EXTENSION IF NOT EXISTS vector;");
                            _context.Database.Migrate();
                            _logger.LogInformation("Migrations applied successfully after loading pgvector.");
                            return;
                        }
                        catch (Exception retryEx)
                        {
                            _logger.LogError(retryEx, "Failed to load pgvector extension or apply migrations after.");
                            throw;
                        }
                    }

                    // Non-retryable migration error — fail fast
                    _logger.LogError(ex, "Migration failed after {Attempt} attempts. Message: {Message}", attempt + 1, msg);
                    throw;
                }
            }

            _logger.LogError("Migration failed after {MaxRetries} retries. Database may be unavailable.", maxRetries);
            throw new InvalidOperationException("Migration failed after retries.");
        }
    }

    private void EnsurePgVectorExtension()
    {
        try
        {
            // Check if vector extension is already loaded by querying pg_extension
            var loaded = _context.Database.ExecuteSqlRaw(
                "SELECT 1 FROM pg_extension WHERE extname = 'vector';");

            if (loaded > 0)
            {
                _logger.LogInformation("pgvector extension already loaded.");
                return;
            }
        }
        catch
        {
            // If the query fails, the extension isn't loaded — fall through to create it
        }

        _context.Database.ExecuteSqlRaw("CREATE EXTENSION IF NOT EXISTS vector;");
        _logger.LogInformation("pgvector extension loaded.");
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
