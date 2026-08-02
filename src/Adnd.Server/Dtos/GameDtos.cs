using Adnd.Server.Models;

namespace Adnd.Server.Dtos;

public record CreateGameDto(
    string Name,
    string SystemId,
    Guid? LLMPresetId,
    string? PlotSeed,
    string? GameParameters,
    string Language);

public record UpdateGameDto(
    string? Name,
    Guid? LLMPresetId,
    string? PlotSeed,
    string? GameParameters,
    string? Language);

public record JoinByCodeRequest(string InviteCode, string CharacterName);

public record PromotePlayerRequest(PlayerRole Role);

public record GameSessionDto(
    Guid Id,
    Guid GameId,
    string? Title,
    string? Description,
    GameSessionStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ClosedAt);

/// <summary>
/// Player as returned to clients. Replaces serializing the Player entity with its User
/// navigation included, which exposed every member's password hash and email address.
/// DisplayName is projected here because the client has always expected it on Player.
/// </summary>
public record PlayerDto(
    Guid Id,
    Guid GameId,
    Guid UserId,
    string CharacterName,
    string DisplayName,
    PlayerRole Role,
    PlayerStatus Status,
    bool IsConnected)
{
    public static PlayerDto From(Player p) => new(
        p.Id, p.GameId, p.UserId, p.CharacterName,
        p.User?.DisplayName ?? string.Empty,
        p.Role, p.Status, p.IsConnected);
}
