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
