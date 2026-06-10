using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Shared;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<LlmPreset> LlmPresets => Set<LlmPreset>();
    public DbSet<GameSystem> GameSystems => Set<GameSystem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.DisplayName).HasMaxLength(64);
            entity.Property(e => e.PasswordHash).HasMaxLength(128);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("NOW()");
        });

        modelBuilder.Entity<LlmPreset>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.CreatedByUserId, e.IsActive });
            entity.Property(e => e.Name).HasMaxLength(128);
            entity.Property(e => e.Provider).HasMaxLength(32);
            entity.Property(e => e.BaseModel).HasMaxLength(128);
            entity.Property(e => e.EmbeddingModel).HasMaxLength(128);
            entity.Property(e => e.ApiKeyEncrypted).IsRequired();
            entity.Property(e => e.SystemPrompt).IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("NOW()");
        });

        modelBuilder.Entity<GameSystem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Slug).IsUnique();
            entity.Property(e => e.Name).HasMaxLength(128);
            entity.Property(e => e.Slug).HasMaxLength(32);
            entity.Property(e => e.Description).HasMaxLength(512);
            entity.Property(e => e.RulesetConfig).HasColumnType("jsonb");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("NOW()");
        });
    }
}
