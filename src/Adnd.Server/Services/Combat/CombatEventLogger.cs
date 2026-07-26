using System.Text.Json;
using Adnd.Server.Data;
using Adnd.Server.Models;
using CombatEntity = Adnd.Server.Models.Combat;

namespace Adnd.Server.Services.Combat;

/// <summary>
/// Shared helper for logging CombatEvent records during combat service writes.
/// Injected as a scoped service to keep logging consistent across all domain services.
/// </summary>
public sealed class CombatEventLogger(AppDbContext db)
{
    /// <summary>
    /// Inserts a CombatEvent record.
    /// Does NOT call SaveChangesAsync — the caller is responsible for the unit of work.
    /// </summary>
    public void Log(
        CombatEntity combat,
        CombatEventType type,
        Guid? actorId,
        Guid? targetId,
        string? content,
        object? metadata = null)
    {
        JsonElement metaElement = default;
        if (metadata is not null)
        {
            var json = JsonSerializer.Serialize(metadata);
            metaElement = JsonSerializer.Deserialize<JsonElement>(json);
        }

        var evt = new CombatEvent
        {
            CombatId = combat.Id,
            Round = combat.CurrentRound,
            Turn = combat.CurrentTurnIndex,
            EventType = type,
            ActorId = actorId,
            TargetId = targetId,
            Content = content,
            Metadata = metaElement,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.CombatEvents.Add(evt);
    }

    /// <summary>
    /// Inserts a CombatEvent record and saves immediately.
    /// Use when the logging is the only DB write in the operation.
    /// </summary>
    public async Task LogAndSaveAsync(
        CombatEntity combat,
        CombatEventType type,
        Guid? actorId,
        Guid? targetId,
        string? content,
        object? metadata = null,
        CancellationToken ct = default)
    {
        Log(combat, type, actorId, targetId, content, metadata);
        await db.SaveChangesAsync(ct);
    }
}
