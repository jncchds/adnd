using Microsoft.EntityFrameworkCore;
using Adnd.Server.Data;
using Adnd.Server.Models;

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

        var user = await _context.Users.FindAsync(userId);

        return new GameResponse
        {
            Id = game.Id,
            CreatorId = game.CreatorId,
            CreatorName = user?.DisplayName ?? user?.Email ?? "Unknown",
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
        var game = await _context.Games.FirstOrDefaultAsync(g => g.Id == id);
        if (game == null || game.CreatorId != userId)
            throw new UnauthorizedAccessException("Cannot delete game.");

        _context.Games.Remove(game);
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

    private string GenerateInviteCode()
    {
        var chars = "abcdefghijklmnopqrstuvwxyz0123456789";
        var random = Random.Shared;
        var code = new string(Enumerable.Repeat(chars, 8)
            .Select(s => s[random.Next(s.Length)]).ToArray());

        if (_context.Games.Any(g => g.InviteCode == code))
            return GenerateInviteCode();

        return code;
    }
}
