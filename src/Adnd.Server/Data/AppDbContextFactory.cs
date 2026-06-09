using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Adnd.Server.Data;

/// <summary>
/// Design-time DbContext factory for EF Core CLI tools (migrations, etc.)
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        // Walk up from the working directory to find appsettings.json
        var currentDir = Directory.GetCurrentDirectory();
        var dir = currentDir;
        while (dir != null)
        {
            var configPath = Path.Combine(dir, "appsettings.json");
            if (File.Exists(configPath))
            {
                var configuration = new ConfigurationBuilder()
                    .SetBasePath(dir)
                    .AddJsonFile(configPath, optional: false)
                    .Build();

                var connectionString = configuration.GetConnectionString("Default")
                    ?? throw new InvalidOperationException("Connection string 'Default' not found in appsettings.json");

                var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
                optionsBuilder.UseNpgsql(connectionString);

                return new AppDbContext(optionsBuilder.Options);
            }
            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new InvalidOperationException("Could not find appsettings.json. Run from the project directory.");
    }
}
