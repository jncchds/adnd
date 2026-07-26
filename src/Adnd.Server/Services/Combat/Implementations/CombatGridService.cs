using System.Text.Json;
using Adnd.Server.Data;
using Adnd.Server.Models;
using Microsoft.EntityFrameworkCore;
using CombatEntity = Adnd.Server.Models.Combat;

namespace Adnd.Server.Services.Combat;

public sealed class CombatGridService(AppDbContext db) : ICombatGridService
{
    // Grid data is stored as a sub-key in Combat.Notes jsonb:
    // { "grid": { "width": 20, "height": 20, "positions": { "<participantId>": { "x": 0, "y": 0 } } } }

    public async Task SetGridAsync(Guid combatId, int width, int height, CancellationToken ct = default)
    {
        var combat = await db.Combats.FindAsync([combatId], ct)
            ?? throw new InvalidOperationException($"Combat {combatId} not found.");

        var notes = ReadNotes(combat);
        notes["grid"] = new GridData(width, height,
            notes.TryGetValue("grid", out var existing)
                ? existing.Positions
                : new Dictionary<string, Position>());

        SaveNotes(combat, notes);
        await db.SaveChangesAsync(ct);
    }

    public async Task SetPositionAsync(Guid participantId, int x, int y, CancellationToken ct = default)
    {
        var participant = await db.CombatParticipants
            .Include(p => p.Combat)
            .FirstOrDefaultAsync(p => p.Id == participantId, ct)
            ?? throw new InvalidOperationException($"Participant {participantId} not found.");

        var notes = ReadNotes(participant.Combat);
        var grid = notes.GetValueOrDefault("grid", new GridData(20, 20, new()));
        grid.Positions[participantId.ToString()] = new Position(x, y);
        notes["grid"] = grid;

        SaveNotes(participant.Combat, notes);
        await db.SaveChangesAsync(ct);
    }

    public async Task MoveAsync(Guid participantId, int dx, int dy, CancellationToken ct = default)
    {
        var participant = await db.CombatParticipants
            .Include(p => p.Combat)
            .FirstOrDefaultAsync(p => p.Id == participantId, ct)
            ?? throw new InvalidOperationException($"Participant {participantId} not found.");

        var notes = ReadNotes(participant.Combat);
        var grid = notes.GetValueOrDefault("grid", new GridData(20, 20, new()));

        var key = participantId.ToString();
        var current = grid.Positions.GetValueOrDefault(key, new Position(0, 0));
        var newX = Math.Max(0, Math.Min(grid.Width - 1, current.X + dx));
        var newY = Math.Max(0, Math.Min(grid.Height - 1, current.Y + dy));
        grid.Positions[key] = new Position(newX, newY);
        notes["grid"] = grid;

        SaveNotes(participant.Combat, notes);
        await db.SaveChangesAsync(ct);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Dictionary<string, GridData> ReadNotes(CombatEntity combat)
    {
        if (combat.Notes.ValueKind == JsonValueKind.Undefined ||
            combat.Notes.ValueKind == JsonValueKind.Null)
            return new();

        return JsonSerializer.Deserialize<Dictionary<string, GridData>>(combat.Notes.GetRawText())
               ?? new();
    }

    private static void SaveNotes(CombatEntity combat, Dictionary<string, GridData> notes)
        => combat.Notes = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(notes));

    private record Position(int X, int Y);

    private record GridData(int Width, int Height, Dictionary<string, Position> Positions);
}
