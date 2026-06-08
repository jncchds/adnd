using System.Data.Common;
using System.Reflection;
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

            // Check if the database is completely empty (no tables at all).
            // This happens on a fresh database after deleting the volume.
            if (IsDatabaseEmpty())
            {
                _logger.LogInformation("Database is empty. Creating initial schema and marking all migrations as applied...");
                _context.Database.EnsureCreated();
                // EnsureCreated() creates everything from the model snapshot, so all migrations are effectively applied.
                // Mark them in the history table so Migrate() knows not to apply them again.
                EnsureMigrationsHistory();
                _logger.LogInformation("Initial schema created and all migrations marked as applied.");
                return;
            }

            ApplyMigrationsWithRetries();
        }
    }

    private bool IsDatabaseEmpty()
    {
        try
        {
            // Check if the EF migrations history table exists.
            // If it doesn't, the database is either fresh or was created with EnsureCreated().
            var exists = _context.Database.ExecuteSqlRaw(
                "SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = '__efmigrationshistory';");
            return exists == 0;
        }
        catch
        {
            // If we can't check, assume empty to be safe
            return true;
        }
    }

    private void EnsureMigrationsHistory()
    {
        // Create the EF migrations history table and mark all known migrations as applied.
        // This is needed because EnsureCreated() doesn't create the history table.
        var knownMigrations = _context.Database.GetMigrations().ToList();

        // Create the history table if it doesn't exist
        try
        {
            var createTableSql = "CREATE TABLE IF NOT EXISTS \"__EFMigrationsHistory\" ("
                + "\"MigrationId\" character varying(150) NOT NULL, "
                + "\"ProductVersion\" character varying(32) NOT NULL, "
                + "CONSTRAINT \"PK___EFMigrationsHistory\" PRIMARY KEY (\"MigrationId\")";
            _context.Database.ExecuteSqlRaw(createTableSql);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create migrations history table. Migrations may need manual intervention.");
            return;
        }

        // Get the EF Core product version
        var productVersion = typeof(MigrationService).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";

        // Mark all known migrations as applied
        foreach (var migration in knownMigrations)
        {
            try
            {
                var insertSql = string.Format(
                    "INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ('{0}', '{1}') ON CONFLICT (\"MigrationId\") DO NOTHING;",
                    migration,
                    productVersion);
                _context.Database.ExecuteSqlRaw(insertSql);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to mark migration {Migration} as applied.", migration);
            }
        }
    }

    private void ApplyMigrationsWithRetries()
    {
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
