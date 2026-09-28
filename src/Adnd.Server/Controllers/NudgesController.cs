using Adnd.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace Adnd.Server.Controllers;

/// <param name="Turns">How many narrator turns it applies to; null keeps it until withdrawn.</param>
public record CreateNudgeRequest(string Text, int? Turns);

/// <summary>
/// The creator's lever on the story — see <see cref="IStoryNudgeService"/>. Creator-only: a
/// nudge goes straight into the GM's prompt, and a player who could write one could steer the
/// story around the rest of the table.
/// </summary>
[Route("api/games/{gameId:guid}/nudges")]
public class NudgesController(
    IStoryNudgeService nudges,
    IGameAuthorizationService auth,
    IUserIdProvider userIdProvider) : GameScopedController(auth, userIdProvider)
{
    [HttpGet]
    public async Task<IActionResult> List(Guid gameId, CancellationToken ct)
    {
        if (await RequireCreator(gameId) is { } failure) return failure;
        return Ok(await nudges.ListAsync(gameId, ct));
    }

    [HttpPost]
    public async Task<IActionResult> Create(Guid gameId, [FromBody] CreateNudgeRequest request, CancellationToken ct)
    {
        if (await RequireCreator(gameId) is { } failure) return failure;
        if (string.IsNullOrWhiteSpace(request.Text))
            return BadRequest(new { error = "Say which way the story should go." });
        if (request.Text.Length > 2000)
            return BadRequest(new { error = "Keep a nudge under 2000 characters." });

        return Ok(await nudges.CreateAsync(gameId, CurrentUserId, request.Text, request.Turns, ct));
    }

    [HttpDelete("{nudgeId:guid}")]
    public async Task<IActionResult> Withdraw(Guid gameId, Guid nudgeId, CancellationToken ct)
    {
        if (await RequireCreator(gameId) is { } failure) return failure;
        return await nudges.WithdrawAsync(gameId, nudgeId, ct)
            ? NoContent()
            : NotFound(new { error = "No active nudge with that id in this game." });
    }
}
