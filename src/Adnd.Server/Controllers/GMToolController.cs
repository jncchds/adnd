using System.Text.Json;
using Adnd.Server.Data;
using Adnd.Server.Events;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Controllers;

public record GMToolDeclineRequest(string? Reason);

/// <param name="FeatureId">The ability to spend, or null to keep the original roll.</param>
public record RerollRequest(string? FeatureId);

public record PendingToolCallDto(
    Guid Id,
    Guid GameId,
    Guid SessionId,
    string ToolName,
    JsonElement Arguments,
    Guid? TargetPlayerId,
    DateTimeOffset StartedAt,
    GMToolCallStatus Status,
    JsonElement Result);

public record GMToolCallSummaryDto(
    Guid Id,
    string ToolName,
    GMToolCallStatus Status,
    JsonElement Arguments,
    JsonElement Result,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    bool RequiresConfirmation,
    Guid? TargetPlayerId);

[ApiController]
[Route("api/gmtools")]
[Authorize]
public class GMToolController(
    AppDbContext db,
    IGMToolRegistry gmToolRegistry,
    IGameAuthorizationService auth,
    IUserIdProvider userIdProvider,
    IRollAnswerService answers) : ControllerBase
{
    /// <summary>List available GM tool definitions.</summary>
    [HttpGet]
    public IActionResult ListTools()
        => Ok(gmToolRegistry.GetToolDefinitions());

    /// <summary>
    /// Tool calls waiting on player confirmation. The client polls this every 5s; without
    /// it the confirmation banner could never appear and gated tools stalled the agent saga.
    /// </summary>
    [HttpGet("pending")]
    public async Task<IActionResult> GetPending([FromQuery] Guid gameId, CancellationToken ct)
    {
        var userId = userIdProvider.GetUserId();
        Player caller;
        try
        {
            caller = await auth.RequirePlayerAsync(gameId, userId);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { error = ex.Message });
        }

        var pending = await db.GMToolCalls
            .AsNoTracking()
            .Where(t => t.GameId == gameId
                && (t.Status == GMToolCallStatus.AwaitingConfirmation
                    // A reroll offer belongs to one player, so unlike a confirmation it is
                    // filtered here. It is listed at all so a dropped SignalR push cannot
                    // strand the turn until the saga's 5-minute timeout.
                    || (t.Status == GMToolCallStatus.AwaitingReroll && t.TargetPlayerId == caller.Id)))
            .OrderBy(t => t.StartedAt)
            .Select(t => new PendingToolCallDto(
                t.Id, t.GameId, t.SessionId, t.ToolName, t.Arguments, t.TargetPlayerId, t.StartedAt,
                t.Status, t.Result))
            .ToListAsync(ct);

        return Ok(pending);
    }

    /// <summary>
    /// Tool calls executed for a single agent call (saga run), for the Agent Calls admin
    /// overview. Authorized against the agent call's own game, not a caller-supplied gameId.
    /// </summary>
    [HttpGet("by-call/{agentCallId:guid}")]
    public async Task<IActionResult> GetByAgentCall(Guid agentCallId, CancellationToken ct)
    {
        var call = await db.AgentCalls.AsNoTracking().FirstOrDefaultAsync(a => a.Id == agentCallId, ct);
        if (call == null) return NotFound();
        if (await RequireCreatorAsync(call.GameId) is { } failure) return failure;

        var toolCalls = await db.GMToolCalls
            .AsNoTracking()
            .Where(t => t.AgentCallId == agentCallId)
            .OrderBy(t => t.ToolIndex).ThenBy(t => t.StartedAt)
            .Select(t => new GMToolCallSummaryDto(
                t.Id, t.ToolName, t.Status, t.Arguments, t.Result,
                t.StartedAt, t.CompletedAt, t.RequiresConfirmation, t.TargetPlayerId))
            .ToListAsync(ct);

        return Ok(toolCalls);
    }

    /// <summary>
    /// Take, or waive, the reroll offered after a requestPlayerRoll. A null featureId keeps
    /// the original roll. Shared with the Telegram bot's buttons (<see cref="IRollAnswerService"/>).
    /// </summary>
    [HttpPost("{id:guid}/reroll")]
    public async Task<IActionResult> Reroll(Guid id, [FromBody] RerollRequest? body, CancellationToken ct)
        => Result(await answers.RerollAsync(id, userIdProvider.GetUserId(), body?.FeatureId, ct));

    [HttpPost("{id:guid}/confirm")]
    public async Task<IActionResult> Confirm(Guid id, CancellationToken ct)
        => Result(await answers.ConfirmAsync(id, userIdProvider.GetUserId(), approved: true, reason: null, ct));

    [HttpPost("{id:guid}/decline")]
    public async Task<IActionResult> Decline(Guid id, [FromBody] GMToolDeclineRequest? body, CancellationToken ct)
        => Result(await answers.ConfirmAsync(id, userIdProvider.GetUserId(), approved: false, reason: body?.Reason, ct));

    private IActionResult Result(RollAnswer answer) => answer switch
    {
        { Ok: true } => NoContent(),
        { NotFound: true } => NotFound(new { error = answer.Error }),
        { Forbidden: true } => StatusCode(403, new { error = answer.Error }),
        _ => BadRequest(new { error = answer.Error }),
    };

    private async Task<IActionResult?> RequireCreatorAsync(Guid gameId)
    {
        try
        {
            await auth.RequireCreatorAsync(gameId, userIdProvider.GetUserId());
            return null;
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { error = ex.Message });
        }
    }
}
