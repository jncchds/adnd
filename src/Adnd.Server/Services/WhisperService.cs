using Adnd.Server.Data;
using Adnd.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Services;

/// <summary>
/// Service for managing whispers (private messages) between players and GM.
/// Supports player-to-player, player-to-GM, and GM-to-player/group whispers.
/// </summary>
public interface IWhisperService
{
    /// <summary>
    /// Send a whisper from a player to specific targets.
    /// Returns the whisper ID for tracking.
    /// </summary>
    Task<Whisper> SendWhisperAsync(Guid gameId, Guid sessionId, Guid fromPlayerId,
        string targets, WhisperType type, string content);

    /// <summary>
    /// Send a GM whisper to specific player(s).
    /// </summary>
    Task<Whisper> SendGMWhisperAsync(Guid gameId, Guid sessionId, Guid fromPlayerId,
        List<Guid> targetPlayerIds, WhisperType type, string content);

    /// <summary>
    /// Get whispers visible to a specific player.
    /// Players can see their own whispers and whispers where they are a target.
    /// GM can see all whispers.
    /// </summary>
    Task<List<Whisper>> GetWhispersForPlayerAsync(Guid gameId, Guid playerId, bool isGM, int limit = 50);

    /// <summary>
    /// Get whispers targeting a specific group name.
    /// </summary>
    Task<List<Whisper>> GetWhispersForGroupAsync(Guid gameId, string groupName, int limit = 50);

    /// <summary>
    /// Check if a player can send a whisper (has permission).
    /// </summary>
    bool CanWhisper(Player player);

    /// <summary>
    /// Get the list of target player IDs from a targets string.
    /// </summary>
    List<Guid> ParseTargets(string targets);

    /// <summary>
    /// Format targets string for storage.
    /// </summary>
    string FormatTargets(List<Guid> playerIds);
}

public class WhisperService : IWhisperService
{
    private readonly AppDbContext _context;
    private readonly ILogger<WhisperService> _logger;

    public WhisperService(AppDbContext context, ILogger<WhisperService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Whisper> SendWhisperAsync(Guid gameId, Guid sessionId, Guid fromPlayerId,
        string targets, WhisperType type, string content)
    {
        var player = await _context.Players
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.Id == fromPlayerId && p.GameId == gameId);

        if (player == null)
            throw new KeyNotFoundException("Player not found in game.");

        if (!CanWhisper(player))
            throw new InvalidOperationException($"Player '{player.CharacterName}' does not have whisper permission.");

        var targetIds = ParseTargets(targets);

        var whisper = new Whisper
        {
            GameId = gameId,
            SessionId = sessionId,
            FromPlayerId = fromPlayerId,
            Targets = targets,
            TargetPlayerIds = targetIds,
            Content = content,
            Type = type,
            CreatedAt = DateTime.UtcNow
        };

        _context.Whispers.Add(whisper);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Whisper sent: from {PlayerId} to [{Targets}] (type: {Type})",
            fromPlayerId, string.Join(", ", targetIds), type);

        return whisper;
    }

    public async Task<Whisper> SendGMWhisperAsync(Guid gameId, Guid sessionId, Guid fromPlayerId,
        List<Guid> targetPlayerIds, WhisperType type, string content)
    {
        var whisper = new Whisper
        {
            GameId = gameId,
            SessionId = sessionId,
            FromPlayerId = fromPlayerId,
            Targets = FormatTargets(targetPlayerIds),
            TargetPlayerIds = targetPlayerIds,
            Content = content,
            Type = type,
            CreatedAt = DateTime.UtcNow
        };

        _context.Whispers.Add(whisper);
        await _context.SaveChangesAsync();

        _logger.LogInformation("GM whisper sent to [{Targets}] (type: {Type})",
            string.Join(", ", targetPlayerIds), type);

        return whisper;
    }

    public async Task<List<Whisper>> GetWhispersForPlayerAsync(Guid gameId, Guid playerId, bool isGM, int limit = 50)
    {
        if (isGM)
        {
            // GM sees all whispers in the game
            return await _context.Whispers
                .Where(w => w.GameId == gameId)
                .Include(w => w.FromPlayer)
                .ThenInclude(p => p.User)
                .OrderByDescending(w => w.CreatedAt)
                .Take(limit)
                .ToListAsync();
        }

        // Players see: whispers they sent, whispers targeting them, and GM whispers to them
        return await _context.Whispers
            .Where(w => w.GameId == gameId &&
                (w.FromPlayerId == playerId ||
                 w.TargetPlayerIds.Contains(playerId) ||
                 w.Targets == "all"))
            .Include(w => w.FromPlayer)
            .ThenInclude(p => p.User)
            .OrderByDescending(w => w.CreatedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<List<Whisper>> GetWhispersForGroupAsync(Guid gameId, string groupName, int limit = 50)
    {
        // Find players in the group
        var groupPlayers = await _context.Players
            .Where(p => p.GameId == gameId && p.WhisperGroups != null && p.WhisperGroups.Contains(groupName))
            .Select(p => p.Id)
            .ToListAsync();

        if (!groupPlayers.Any())
            return new List<Whisper>();

        return await _context.Whispers
            .Where(w => w.GameId == gameId && w.Targets.Contains($"group:{groupName}"))
            .Include(w => w.FromPlayer)
            .ThenInclude(p => p.User)
            .OrderByDescending(w => w.CreatedAt)
            .Take(limit)
            .ToListAsync();
    }

    public bool CanWhisper(Player player)
    {
        return player.CanWhisper;
    }

    public List<Guid> ParseTargets(string targets)
    {
        if (string.IsNullOrEmpty(targets))
            return new List<Guid>();

        if (targets == "all")
            return new List<Guid>();

        // Format: "player:{id1},player:{id2},group:{name}"
        var result = new List<Guid>();
        var parts = targets.Split(',', StringSplitOptions.RemoveEmptyEntries);

        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (trimmed.StartsWith("player:", StringComparison.OrdinalIgnoreCase))
            {
                if (Guid.TryParse(trimmed.Substring(7), out var id))
                    result.Add(id);
            }
            // group targets stay as strings, not parsed to Guids
        }

        return result;
    }

    public string FormatTargets(List<Guid> playerIds)
    {
        return string.Join(",", playerIds.Select(id => $"player:{id}"));
    }
}
