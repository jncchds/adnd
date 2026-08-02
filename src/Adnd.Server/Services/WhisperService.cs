using System.Text.Json;
using Adnd.Server.Data;
using Adnd.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Services;

public interface IWhisperService
{
    Task<List<Whisper>> GetWhisperHistoryAsync(Guid sessionId, Guid playerId);
    Task<Whisper> CreateWhisperAsync(Guid sessionId, Guid? fromPlayerId, WhisperType type, string content, List<Guid> targetPlayerIds);
}

public class WhisperService(AppDbContext db) : IWhisperService
{
    /// <summary>
    /// Whispers visible to one player: the ones they sent and the ones addressed to them.
    /// The playerId argument used to be ignored entirely, so this returned every private
    /// message in the session to anyone who asked.
    /// </summary>
    public async Task<List<Whisper>> GetWhisperHistoryAsync(Guid sessionId, Guid playerId)
    {
        var whispers = await db.Whispers
            .AsNoTracking()
            .Where(w => w.SessionId == sessionId)
            .OrderByDescending(w => w.CreatedAt)
            .Take(200)
            .ToListAsync();

        // TargetPlayerIds is a jsonb array, so the recipient test is applied in memory.
        return whispers
            .Where(w => w.FromPlayerId == playerId || IsAddressedTo(w, playerId))
            .Take(50)
            .ToList();
    }

    private static bool IsAddressedTo(Whisper whisper, Guid playerId)
    {
        if (whisper.TargetPlayerIds.ValueKind != JsonValueKind.Array) return false;

        foreach (var element in whisper.TargetPlayerIds.EnumerateArray())
        {
            if (element.TryGetGuid(out var id) && id == playerId) return true;
        }
        return false;
    }

    public async Task<Whisper> CreateWhisperAsync(Guid sessionId, Guid? fromPlayerId, WhisperType type, string content, List<Guid> targetPlayerIds)
    {
        var whisper = new Whisper
        {
            SessionId = sessionId,
            FromPlayerId = fromPlayerId,
            Type = type,
            Content = content,
            TargetPlayerIds = JsonSerializer.SerializeToElement(targetPlayerIds)
        };
        db.Whispers.Add(whisper);
        await db.SaveChangesAsync();
        return whisper;
    }
}
