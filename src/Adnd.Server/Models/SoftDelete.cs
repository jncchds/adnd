using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Models;

/// <summary>
/// Mixin interface for soft-delete support across all entities.
/// Adds IsDeleted flag and DeletedAt timestamp for soft-delete pattern.
/// </summary>
public interface ISoftDelete
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAt { get; set; }
}

/// <summary>
/// Helper methods for soft-delete operations.
/// </summary>
public static class SoftDeleteExtensions
{
    /// <summary>
    /// Soft-delete an entity by marking it as deleted.
    /// </summary>
    public static void SoftDelete(this ISoftDelete entity)
    {
        entity.IsDeleted = true;
        entity.DeletedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Restore a soft-deleted entity.
    /// </summary>
    public static void Restore(this ISoftDelete entity)
    {
        entity.IsDeleted = false;
        entity.DeletedAt = null;
    }

    /// <summary>
    /// EF Core query filter to automatically exclude soft-deleted entities.
    /// Apply this in OnModelCreating for all entities implementing ISoftDelete.
    /// </summary>
    public static void ConfigureSoftDelete<T>(this ModelBuilder modelBuilder) where T : class, ISoftDelete
    {
        modelBuilder.Entity<T>()
            .HasQueryFilter(e => !e.IsDeleted);
    }
}
