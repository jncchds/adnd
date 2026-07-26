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
    public async Task<List<Whisper>> GetWhisperHistoryAsync(Guid sessionId, Guid playerId)
        => await db.Whispers
            .Where(w => w.SessionId == sessionId)
            .OrderByDescending(w => w.CreatedAt)
            .Take(50)
            .ToListAsync();

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
