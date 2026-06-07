using Microsoft.EntityFrameworkCore;
using Adnd.Server.Models;

namespace Adnd.Server.Data;

/// <summary>
/// Extension methods for common DbContext patterns.
/// Replaces repeated FindAsync + null check boilerplate.
/// </summary>
public static class GameExtensions
{
    public static async Task<T?> FindOrFailAsync<T>(this DbSet<T> dbSet, Guid id) where T : class
    {
        var entity = await dbSet.FindAsync(id);
        if (entity == null)
            throw new KeyNotFoundException($"Entity of type {typeof(T).Name} with id {id} not found.");
        return entity;
    }

    public static async Task<T> FindOrFailAsync<T>(this DbSet<T> dbSet, Guid id, string entityName) where T : class
    {
        var entity = await dbSet.FindAsync(id);
        if (entity == null)
            throw new KeyNotFoundException($"{entityName} with id {id} not found.");
        return entity;
    }
}
