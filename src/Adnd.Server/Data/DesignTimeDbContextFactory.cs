using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Adnd.Server.Data;

/// <summary>
/// Builds an AppDbContext for `dotnet ef` without running Program.cs.
///
/// Without this, the EF tooling boots the full host, which enforces production startup
/// requirements (a real JWT signing key, an encryption master key) that the migrations
/// tooling has no business needing. Scaffolding a migration only requires a connection
/// string and the model.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Host=postgres;Database=adnd;Username=adnd;Password=adnd";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString, o => o.UseVector())
            .Options;

        return new AppDbContext(options);
    }
}
