using Microsoft.EntityFrameworkCore;
using Adnd.Server.Data;
using Adnd.Server.Models;

namespace Adnd.Server.Services;

/// <summary>
/// Strategy interface for game join flows.
/// Different join methods (invite code, direct join, admin invite) have different validation and setup steps.
/// </summary>
public interface IGameJoinStrategy
{
    /// <summary>
    /// Gets the strategy name (used for logging/identification).
    /// </summary>
    string StrategyName { get; }

    /// <summary>
    /// Validates the join request before processing.
    /// </summary>
    (bool valid, string? error) Validate(Game game, string userId);

    /// <summary>
    /// Processes the game join and returns the result.
    /// </summary>
    Task<JoinResult> ProcessJoinAsync(Game game, string userId, string characterName);
}

/// <summary>
/// Factory for creating game join strategies.
/// </summary>
public interface IGameJoinFactory
{
    IGameJoinStrategy GetStrategy(string joinType);
    void RegisterStrategy(IGameJoinStrategy strategy);
}

/// <summary>
/// Result of a game join operation.
/// </summary>
public class JoinResult
{
    public Guid PlayerId { get; init; }
    public string CharacterName { get; init; } = string.Empty;
    public bool Success { get; init; }
    public string? Error { get; init; }
}

// ============================================================================
// Concrete Join Strategies
// ============================================================================

/// <summary>
/// Strategy for joining a game by invite code.
/// Includes additional validation: checks if game is active.
/// </summary>
public class InviteCodeJoinStrategy : IGameJoinStrategy
{
    public string StrategyName => "invite-code";

    public (bool valid, string? error) Validate(Game game, string userId)
    {
        if (game.Status != GameStatus.Active)
            return (false, "This game is not currently active.");

        if (!Guid.TryParse(userId, out var uid))
            return (false, "Invalid user ID.");

        if (game.Players.Any(p => p.UserId == uid))
            return (false, "You are already a player in this game.");

        return (true, null);
    }

    public async Task<JoinResult> ProcessJoinAsync(Game game, string userId, string characterName)
    {
        if (!Guid.TryParse(userId, out var uid))
            return new JoinResult { Success = false, Error = "Invalid user ID." };

        var player = new Player
        {
            GameId = game.Id,
            UserId = uid,
            CharacterName = characterName,
            Role = PlayerRole.Player,
            Status = PlayerStatus.Active,
            JoinedAt = DateTime.UtcNow
        };

        game.Players.Add(player);

        return new JoinResult
        {
            PlayerId = player.Id,
            CharacterName = player.CharacterName,
            Success = true
        };
    }
}

/// <summary>
/// Strategy for direct game join (post-join endpoint).
/// Simpler validation: just checks if user is already a player.
/// </summary>
public class DirectJoinStrategy : IGameJoinStrategy
{
    public string StrategyName => "direct";

    public (bool valid, string? error) Validate(Game game, string userId)
    {
        if (!Guid.TryParse(userId, out var uid))
            return (false, "Invalid user ID.");

        if (game.Players.Any(p => p.UserId == uid))
            return (false, "You are already a player in this game.");

        return (true, null);
    }

    public async Task<JoinResult> ProcessJoinAsync(Game game, string userId, string characterName)
    {
        if (!Guid.TryParse(userId, out var uid))
            return new JoinResult { Success = false, Error = "Invalid user ID." };

        var player = new Player
        {
            GameId = game.Id,
            UserId = uid,
            CharacterName = characterName,
            Role = PlayerRole.Player,
            Status = PlayerStatus.Active,
            JoinedAt = DateTime.UtcNow
        };

        game.Players.Add(player);

        return new JoinResult
        {
            PlayerId = player.Id,
            CharacterName = player.CharacterName,
            Success = true
        };
    }
}

/// <summary>
/// Factory implementation for game join strategies.
/// </summary>
public class GameJoinFactory : IGameJoinFactory
{
    private readonly Dictionary<string, IGameJoinStrategy> _strategies = new();

    public GameJoinFactory()
    {
        RegisterStrategy(new InviteCodeJoinStrategy());
        RegisterStrategy(new DirectJoinStrategy());
    }

    public IGameJoinStrategy GetStrategy(string joinType)
    {
        if (_strategies.TryGetValue(joinType, out var strategy))
            return strategy;

        // Default to direct join
        return _strategies["direct"];
    }

    public void RegisterStrategy(IGameJoinStrategy strategy)
    {
        _strategies[strategy.StrategyName] = strategy;
    }
}
