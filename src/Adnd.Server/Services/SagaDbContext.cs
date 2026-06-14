using Microsoft.EntityFrameworkCore;
using Adnd.Server.Models;

namespace Adnd.Server.Services;

/// <summary>
/// EF Core context for saga persistence.
/// Maps AgentSagaData to PostgreSQL — survives container restarts.
/// </summary>
public class SagaDbContext : DbContext
{
    public SagaDbContext(DbContextOptions<SagaDbContext> options) : base(options) { }

    public DbSet<AgentSagaData> AgentSagas { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AgentSagaData>(b =>
        {
            b.HasKey(s => s.Id);
            b.Property(s => s.CurrentState).HasMaxLength(128);
            b.Property(s => s.CorrelationId).HasMaxLength(128);
        });
    }
}
