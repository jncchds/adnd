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
