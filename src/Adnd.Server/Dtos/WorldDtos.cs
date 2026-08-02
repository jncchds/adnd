using Adnd.Server.Models;

namespace Adnd.Server.Dtos;

// Request shapes for NPCs and plot threads. These endpoints used to bind the EF entities
// straight from the body, which let the caller set Id, GameId and the soft-delete flags.

public record CreateNPCDto(
    Guid GameId,
    string Name,
    string? Description,
    Attitude Attitude = Attitude.Neutral,
    string? Faction = null);

public record UpdateNPCDto(
    string? Name,
    string? Description,
    Attitude? Attitude,
    string? Faction);

public record CreatePlotThreadDto(
    Guid GameId,
    string Title,
    string? Description,
    PlotThreadCategory Category = PlotThreadCategory.General,
    PlotThreadStatus Status = PlotThreadStatus.Active,
    float Momentum = 0f,
    string? NextMilestone = null,
    string? Foreshadowing = null,
    bool IsDynamic = true);

public record UpdatePlotThreadDto(
    string? Title,
    string? Description,
    PlotThreadCategory? Category,
    PlotThreadStatus? Status,
    float? Momentum,
    string? NextMilestone,
    string? Foreshadowing,
    bool? IsDynamic);
