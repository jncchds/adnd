using Microsoft.EntityFrameworkCore;
using Adnd.Server.Data;
using Adnd.Server.Models;
using System.Security.Cryptography;

namespace Adnd.Server.Services;

/// <summary>
/// Service for game CRUD operations.
/// </summary>
public interface IGameManagementService
{
    Task<List<GameResponse>> GetGamesAsync(string userId);
    Task<GameResponse?> GetGameAsync(Guid id, string userId);
    Task<GameResponse> CreateGameAsync(Guid userId, CreateGameRequest request);
    Task DeleteGameAsync(Guid id, Guid userId);
    Task<InviteResponse> GenerateInviteAsync(Guid id, Guid userId);
    Task UpdateGameLanguageAsync(Guid id, Guid userId, string language);
    /// <summary>Checks whether the LLM preset can be changed for a game.</summary>
    Task<bool> CanChangeLLMPresetAsync(Guid gameId, Guid userId);
}

public class GameManagementService : IGameManagementService
{
    private readonly AppDbContext _context;
    private readonly ILogger<GameManagementService> _logger;

    public GameManagementService(AppDbContext context, ILogger<GameManagementService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<GameResponse>> GetGamesAsync(string userId)
    {
        if (!Guid.TryParse(userId, out var id))
            return new();

        return await _context.Games
            .Where(g => g.CreatorId == id || g.Players.Any(p => p.UserId == id))
            .Select(g => new GameResponse
            {
                Id = g.Id,
                CreatorId = g.CreatorId,
                CreatorName = g.Creator != null ? (g.Creator.DisplayName ?? g.Creator.Email) : "Unknown",
                Name = g.Name,
                SystemId = g.SystemId,
                SystemVersion = g.SystemVersion,
                Status = g.Status,
                GMStatus = g.GMStatus,
                CreatedAt = g.CreatedAt,
                InviteCode = g.InviteCode,
                LLMPresetId = g.LLMPresetId,
                LLMPresetName = g.LLMPreset != null ? g.LLMPreset.Name : null,
                Language = g.Language
            })
            .ToListAsync();
    }

    public async Task<GameResponse?> GetGameAsync(Guid id, string userId)
    {
        var game = await _context.Games
            .Include(g => g.Creator)
            .Include(g => g.LLMPreset)
            .FirstOrDefaultAsync(g => g.Id == id);

        if (game == null)
            return null;

        if (userId != null && Guid.TryParse(userId, out var uid))
        {
            var hasAccess = game.CreatorId == uid || game.Players.Any(p => p.UserId == uid);
            if (!hasAccess)
                return null;
        }

        return new GameResponse
        {
            Id = game.Id,
            CreatorId = game.CreatorId,
            CreatorName = game.Creator!.DisplayName ?? game.Creator.Email,
            Name = game.Name,
            SystemId = game.SystemId,
            SystemVersion = game.SystemVersion,
            Status = game.Status,
            GMStatus = game.GMStatus,
            CreatedAt = game.CreatedAt,
            InviteCode = game.InviteCode,
            PlotSeed = game.PlotSeed,
            GameParameters = game.GameParameters,
            GameState = game.GameState,
            LLMPresetId = game.LLMPresetId,
            LLMPresetName = game.LLMPreset?.Name,
            Language = game.Language
        };
    }

    public async Task<GameResponse> CreateGameAsync(Guid userId, CreateGameRequest request)
    {
        // Validate LLM preset ownership if one was provided
        if (request.LLMPresetId.HasValue)
        {
            var preset = await _context.LLMPresets
                .FirstOrDefaultAsync(p => p.Id == request.LLMPresetId.Value && p.UserId == userId);
            if (preset == null)
                throw new ArgumentException("Selected LLM preset does not belong to you or does not exist.");
        }

        var game = new Game
        {
            CreatorId = userId,
            Name = request.Name,
            SystemId = request.SystemId,
            SystemVersion = request.SystemVersion,
            CustomSystemJson = request.CustomSystemJson,
            PlotSeed = request.PlotSeed,
            GameParameters = request.GameParameters,
            LLMPresetId = request.LLMPresetId,
            Language = request.Language ?? "English",
            GMStatus = GMStatus.Idle,
            Status = GameStatus.Draft,
            CreatedAt = DateTime.UtcNow
        };

        game.InviteCode = GenerateInviteCode();

        _context.Games.Add(game);
        await _context.SaveChangesAsync();

        return new GameResponse
        {
            Id = game.Id,
            CreatorId = game.CreatorId,
            CreatorName = "Creator",
            Name = game.Name,
            SystemId = game.SystemId,
            SystemVersion = game.SystemVersion,
            Status = game.Status,
            GMStatus = game.GMStatus,
            CreatedAt = game.CreatedAt,
            InviteCode = game.InviteCode,
            PlotSeed = game.PlotSeed,
            GameParameters = game.GameParameters,
            GameState = game.GameState,
            LLMPresetId = game.LLMPresetId,
            LLMPresetName = game.LLMPreset?.Name,
            Language = game.Language
        };
    }

    public async Task DeleteGameAsync(Guid id, Guid userId)
    {
        var game = await _context.Games
            .Include(g => g.AgentCalls)
            .Include(g => g.PlotThreads)
            .Include(g => g.Players)
            .FirstOrDefaultAsync(g => g.Id == id);
        if (game == null || game.CreatorId != userId)
            throw new UnauthorizedAccessException("Cannot delete game.");

        // Set FKs to Guid.Empty instead of cascade delete to preserve related data
        foreach (var call in game.AgentCalls)
        {
            call.GameId = Guid.Empty;
        }
        foreach (var thread in game.PlotThreads)
        {
            thread.GameId = Guid.Empty;
        }
        // Deactivate players rather than deleting them
        foreach (var player in game.Players)
        {
            player.Status = PlayerStatus.Left;
            player.LeftAt = DateTime.UtcNow;
        }

        // Query and fix FKs for entities without navigation on Game model
        var gmToolCalls = await _context.GMToolCalls.Where(g => g.GameId == id).ToListAsync();
        foreach (var tc in gmToolCalls) tc.GameId = Guid.Empty;

        var messages = await _context.Messages.Where(m => m.SessionId != Guid.Empty &&
            _context.GameSessions.Any(s => s.Id == m.SessionId && s.GameId == id)).ToListAsync();
        foreach (var m in messages) m.SessionId = Guid.Empty;

        var llmLogs = await _context.LLMInteractionLogs.Where(l => l.OriginGameId == id).ToListAsync();
        foreach (var log in llmLogs) log.OriginGameId = Guid.Empty;

        game.Status = GameStatus.Archived;
        await _context.SaveChangesAsync();
    }

    public async Task<InviteResponse> GenerateInviteAsync(Guid id, Guid userId)
    {
        var game = await _context.Games.FirstOrDefaultAsync(g => g.Id == id);
        if (game == null || game.CreatorId != userId)
            throw new UnauthorizedAccessException("Cannot generate invite.");

        game.InviteCode = GenerateInviteCode();
        await _context.SaveChangesAsync();

        return new InviteResponse
        {
            InviteCode = game.InviteCode!,
            InviteUrl = $"/join/{game.InviteCode}"
        };
    }

    public async Task UpdateGameLanguageAsync(Guid id, Guid userId, string language)
    {
        var game = await _context.Games.FirstOrDefaultAsync(g => g.Id == id);
        if (game == null || game.CreatorId != userId)
            throw new UnauthorizedAccessException("Cannot update language.");

        game.Language = language ?? "English";
        await _context.SaveChangesAsync();
    }

    public async Task<bool> CanChangeLLMPresetAsync(Guid gameId, Guid userId)
    {
        var game = await _context.Games
            .FirstOrDefaultAsync(g => g.Id == gameId && g.CreatorId == userId);

        if (game == null)
            return false;

        // Cannot change preset once the game has started
        return game.Status != GameStatus.Active;
    }

    private string GenerateInviteCode()
    {
        // Datetime concat + random approach: prevents full table scan for collision check
        // Format: YYYYMMDDHHmm + 4 random chars (e.g., 202606111430a3f9)
        var baseCode = DateTime.UtcNow.ToString("yyyyMMddHHmm");
        var randomPart = Convert.ToHexString(RandomNumberGenerator.GetBytes(4)).ToLowerInvariant().Replace("0", "a").Replace("O", "b").Substring(0, 4);
        var code = $"{baseCode}{randomPart}";

        // Collision check is now index-friendly: prefix search on the datetime portion
        // With proper indexing, this avoids full table scan
        if (_context.Games.Any(g => g.InviteCode == code))
            return GenerateInviteCode();

        return code;
    }
}
