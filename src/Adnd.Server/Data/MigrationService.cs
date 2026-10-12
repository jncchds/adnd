using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Data;

public class MigrationService(AppDbContext db, ILogger<MigrationService> logger)
{
    public async Task ApplyMigrationsAsync()
    {
        try
        {
            logger.LogInformation("Applying database migrations...");
            await db.Database.MigrateAsync();

            // On a brand-new database the pgvector extension is created by the first
            // migration, after Npgsql has already read the type catalogue — so "vector" was
            // unknown to the data source until the next restart and every embedding write
            // failed, taking the first game start down with it.
            var connection = (Npgsql.NpgsqlConnection)db.Database.GetDbConnection();
            await db.Database.OpenConnectionAsync();
            await connection.ReloadTypesAsync();
            await db.Database.CloseConnectionAsync();

            logger.LogInformation("Database migrations applied successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to apply database migrations.");
            throw;
        }
    }
}

public static class MigrationExtensions
{
    public static async Task UseDatabaseMigrationsAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<MigrationService>();
        await svc.ApplyMigrationsAsync();
    }
}
