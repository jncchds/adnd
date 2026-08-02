using System.Security.Cryptography;
using Adnd.Server.Data;
using Adnd.Server.Dtos;
using Adnd.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Services;

public interface IGameManagementService
{
    Task<List<Game>> GetUserGamesAsync(Guid userId);
    Task<List<Game>> GetArchivedUserGamesAsync(Guid userId);
    Task<Game> GetByIdAsync(Guid gameId, Guid userId);
    Task<Game> CreateAsync(Guid userId, CreateGameDto dto);
    Task<Game> UpdateAsync(Guid gameId, Guid userId, UpdateGameDto dto);
    Task DeleteAsync(Guid gameId, Guid userId);
    Task<string> GenerateInviteCodeAsync(Guid gameId, Guid userId);
    Task<Game> JoinByCodeAsync(string inviteCode, Guid userId, string characterName);
    Task StartAsync(Guid gameId, Guid userId);
    Task ArchiveAsync(Guid gameId, Guid userId);
    Task<List<Player>> GetPlayersAsync(Guid gameId, Guid userId);
    Task PromotePlayerAsync(Guid gameId, Guid targetPlayerId, PlayerRole newRole, Guid requesterId);
    Task KickPlayerAsync(Guid gameId, Guid targetPlayerId, Guid requesterId);
    Task<List<GameSession>> GetSessionsAsync(Guid gameId, Guid userId, CancellationToken ct = default);
}

public class GameManagementService(
    AppDbContext db,
    IGameAuthorizationService auth) : IGameManagementService
{
    public async Task<List<Game>> GetUserGamesAsync(Guid userId)
    {
        return await db.Games
            .Include(g => g.Players)
            .Where(g => g.Players.Any(p => p.UserId == userId))
            .OrderBy(g => g.Name)
            .ToListAsync();
    }

    public async Task<List<Game>> GetArchivedUserGamesAsync(Guid userId)
    {
        return await db.Games
            .IgnoreQueryFilters()
            .Include(g => g.Players)
            .Where(g => g.IsDeleted && g.Players.Any(p => p.UserId == userId))
            .OrderByDescending(g => g.DeletedAt)
            .ToListAsync();
    }

    public async Task<Game> GetByIdAsync(Guid gameId, Guid userId)
    {
        await auth.RequirePlayerAsync(gameId, userId);

        return await db.Games
            .Include(g => g.Players)
            .FirstOrDefaultAsync(g => g.Id == gameId)
            ?? throw new KeyNotFoundException($"Game {gameId} not found.");
    }

    public async Task<Game> CreateAsync(Guid userId, CreateGameDto dto)
    {
        var game = new Game
        {
            CreatorId = userId,
            Name = dto.Name,
            SystemId = dto.SystemId,
            LLMPresetId = dto.LLMPresetId,
            PlotSeed = dto.PlotSeed,
            GameParameters = dto.GameParameters,
            Language = dto.Language
        };

        db.Games.Add(game);

        // The Creator is a table admin who can also play — not the GM. The GM is the LLM and
        // has no Player row at all. Labeling this row "Game Master" leaked into RAGService's
        // prompt context (falls back to "GM" only when Player is null), making the real GM
        // think the human creator was itself the GM.
        var creatorUser = await db.Users.FindAsync(userId);
        var player = new Player
        {
            GameId = game.Id,
            UserId = userId,
            CharacterName = creatorUser?.DisplayName ?? "Creator",
            Role = PlayerRole.Creator
        };

        db.Players.Add(player);
        await db.SaveChangesAsync();

        return game;
    }

    public async Task<Game> UpdateAsync(Guid gameId, Guid userId, UpdateGameDto dto)
    {
        await auth.RequireCreatorAsync(gameId, userId);

        var game = await db.Games.FindAsync(gameId)
            ?? throw new KeyNotFoundException($"Game {gameId} not found.");

        if (dto.Name is not null) game.Name = dto.Name;
        if (dto.LLMPresetId.HasValue) game.LLMPresetId = dto.LLMPresetId.Value;
        if (dto.PlotSeed is not null) game.PlotSeed = dto.PlotSeed;
        if (dto.GameParameters is not null) game.GameParameters = dto.GameParameters;
        if (dto.Language is not null) game.Language = dto.Language;
        game.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync();
        return game;
    }

    public async Task DeleteAsync(Guid gameId, Guid userId)
    {
        await auth.RequireCreatorAsync(gameId, userId);

        var game = await db.Games.FindAsync(gameId)
            ?? throw new KeyNotFoundException($"Game {gameId} not found.");

        game.IsDeleted = true;
        game.DeletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task<string> GenerateInviteCodeAsync(Guid gameId, Guid userId)
    {
        await auth.RequireCreatorAsync(gameId, userId);

        var game = await db.Games.FindAsync(gameId)
            ?? throw new KeyNotFoundException($"Game {gameId} not found.");

        string code;
        do
        {
            code = GenerateRandomCode(8);
        } while (await db.Games.AnyAsync(g => g.InviteCode == code));

        game.InviteCode = code;
        game.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        return code;
    }

    public async Task<Game> JoinByCodeAsync(string inviteCode, Guid userId, string characterName)
    {
        var game = await db.Games
            .FirstOrDefaultAsync(g => g.InviteCode == inviteCode.ToLower())
            ?? throw new KeyNotFoundException("Game not found for the given invite code.");

        var existing = await db.Players
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.GameId == game.Id && p.UserId == userId);

        if (existing is not null)
            throw new InvalidOperationException("User is already a member of this game.");

        var player = new Player
        {
            GameId = game.Id,
            UserId = userId,
            CharacterName = characterName,
            Role = PlayerRole.Player
        };

        db.Players.Add(player);
        await db.SaveChangesAsync();

        return game;
    }

    public async Task StartAsync(Guid gameId, Guid userId)
    {
        await auth.RequireCreatorAsync(gameId, userId);

        var game = await db.Games.FindAsync(gameId)
            ?? throw new KeyNotFoundException($"Game {gameId} not found.");

        game.Status = GameStatus.Starting;
        game.GMStatus = GMStatus.Running;
        game.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task ArchiveAsync(Guid gameId, Guid userId)
    {
        await auth.RequireCreatorAsync(gameId, userId);

        var game = await db.Games.FindAsync(gameId)
            ?? throw new KeyNotFoundException($"Game {gameId} not found.");

        game.Status = GameStatus.Archived;
        game.IsDeleted = true;
        game.DeletedAt = DateTimeOffset.UtcNow;
        game.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task<List<GameSession>> GetSessionsAsync(Guid gameId, Guid userId, CancellationToken ct = default)
    {
        await auth.RequirePlayerAsync(gameId, userId);

        return await db.GameSessions
            .AsNoTracking()
            .Where(s => s.GameId == gameId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<List<Player>> GetPlayersAsync(Guid gameId, Guid userId)
    {
        await auth.RequirePlayerAsync(gameId, userId);

        return await db.Players
            .Include(p => p.User)
            .Where(p => p.GameId == gameId)
            .OrderBy(p => p.Role)
            .ThenBy(p => p.CharacterName)
            .ToListAsync();
    }

    public async Task PromotePlayerAsync(Guid gameId, Guid targetPlayerId, PlayerRole newRole, Guid requesterId)
    {
        await auth.RequireCreatorAsync(gameId, requesterId);

        var target = await db.Players
            .FirstOrDefaultAsync(p => p.Id == targetPlayerId && p.GameId == gameId)
            ?? throw new KeyNotFoundException($"Player {targetPlayerId} not found in game {gameId}.");

        target.Role = newRole;
        await db.SaveChangesAsync();
    }

    public async Task KickPlayerAsync(Guid gameId, Guid targetPlayerId, Guid requesterId)
    {
        await auth.RequireCreatorAsync(gameId, requesterId);

        var target = await db.Players
            .FirstOrDefaultAsync(p => p.Id == targetPlayerId && p.GameId == gameId)
            ?? throw new KeyNotFoundException($"Player {targetPlayerId} not found in game {gameId}.");

        target.IsDeleted = true;
        target.DeletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Invite codes are a bearer credential for joining a game, so they must come from a
    /// cryptographic RNG — Random.Shared is predictable from observed output.
    /// </summary>
    private static string GenerateRandomCode(int length)
    {
        const string Chars = "abcdefghijklmnopqrstuvwxyz0123456789";
        return RandomNumberGenerator.GetString(Chars, length);
    }
}
