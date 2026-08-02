using Adnd.Server.Models;
using Adnd.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Adnd.Server.Controllers;

/// <summary>
/// Base for controllers that operate on a specific game.
///
/// Most endpoints in this app took a caller-supplied gameId (or an entity id belonging to
/// a game) and queried it with no membership check at all, so any authenticated user could
/// read and overwrite any other user's game data by id. Deriving from this puts the checks
/// one call away and gives every such controller the same failure shape.
/// </summary>
[ApiController]
[Authorize]
public abstract class GameScopedController(
    IGameAuthorizationService auth,
    IUserIdProvider userIdProvider) : ControllerBase
{
    protected Guid CurrentUserId => userIdProvider.GetUserId();

    /// <summary>
    /// Returns null when the caller is a member of the game, otherwise the error result to
    /// return. Usage: <c>if (await RequireMember(gameId) is { } failure) return failure;</c>
    /// </summary>
    protected async Task<IActionResult?> RequireMember(Guid gameId)
    {
        try
        {
            await auth.RequirePlayerAsync(gameId, CurrentUserId);
            return null;
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>As <see cref="RequireMember"/>, but only the game's creator (the GM) passes.</summary>
    protected async Task<IActionResult?> RequireCreator(Guid gameId)
    {
        try
        {
            await auth.RequireCreatorAsync(gameId, CurrentUserId);
            return null;
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>The caller's Player row in this game, or an error result.</summary>
    protected async Task<(Player? Player, IActionResult? Failure)> ResolvePlayer(Guid gameId)
    {
        try
        {
            return (await auth.RequirePlayerAsync(gameId, CurrentUserId), null);
        }
        catch (UnauthorizedAccessException ex)
        {
            return (null, StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message }));
        }
    }
}
