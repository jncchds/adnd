using Adnd.Server.Data;
using Adnd.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Services;

/// <summary>
/// One narrator turn at a time per game. Every in-character line starts a turn, and a turn is
/// many model calls long, so a second player speaking mid-turn used to start a second saga
/// against the same fight: two models advancing one turn order, each blind to the other.
/// A line that arrives while a turn is running stays <c>Pending</c> and is started when that
/// turn ends, oldest first.
/// </summary>
public interface IGmTurnQueue
{
    /// <summary>Marks the call Running and returns it — or null when it has to wait for the
    /// turn in progress, or has already been started by another delivery.</summary>
    Task<AgentCall?> TryBeginAsync(Guid agentCallId, CancellationToken ct = default);

    /// <summary>The narrator turn that should start now that one has ended, if any.</summary>
    Task<Guid?> NextAsync(Guid gameId, CancellationToken ct = default);
}

public class GmTurnQueue(AppDbContext db) : IGmTurnQueue
{
    /// <summary>A turn that has not moved for this long was abandoned by a crash, not by the
    /// saga — whose own timeout is five minutes a step — and must not block the game.</summary>
    private static readonly TimeSpan Stale = TimeSpan.FromMinutes(10);

    /// <summary>A line nobody answered for this long is no longer worth a turn.</summary>
    private static readonly TimeSpan Expired = TimeSpan.FromMinutes(30);

    public async Task<AgentCall?> TryBeginAsync(Guid agentCallId, CancellationToken ct = default)
    {
        var call = await db.AgentCalls.FindAsync([agentCallId], ct);
        if (call == null) return null;

        if (call.Action != AgentAction.Narrate)
        {
            call.Status = AgentCallStatus.Running;
            await db.SaveChangesAsync(ct);
            return call;
        }

        // Two lines sent in the same instant both see "nobody is running" unless the check and
        // the claim are one step; the lock is held until the transaction commits.
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var key = BitConverter.ToInt64(call.GameId.ToByteArray(), 0);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})", ct);
        await db.Entry(call).ReloadAsync(ct);

        if (call.Status != AgentCallStatus.Pending || await IsBusyAsync(call.GameId, ct))
        {
            await tx.CommitAsync(ct);
            return null;
        }

        call.Status = AgentCallStatus.Running;
        call.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return call;
    }

    public async Task<Guid?> NextAsync(Guid gameId, CancellationToken ct = default)
    {
        if (await IsBusyAsync(gameId, ct)) return null;

        var since = DateTimeOffset.UtcNow - Expired;
        return await db.AgentCalls
            .Where(c => c.GameId == gameId && c.Action == AgentAction.Narrate
                && c.Status == AgentCallStatus.Pending && c.CreatedAt > since)
            .OrderBy(c => c.CreatedAt)
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(ct);
    }

    private Task<bool> IsBusyAsync(Guid gameId, CancellationToken ct)
    {
        var alive = DateTimeOffset.UtcNow - Stale;
        return db.AgentCalls.AnyAsync(c => c.GameId == gameId && c.Action == AgentAction.Narrate
            && c.Status == AgentCallStatus.Running && c.UpdatedAt > alive, ct);
    }
}
