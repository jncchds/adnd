using System.Text.Json;
using Adnd.Server.Data;
using Adnd.Server.Events;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Controllers;

public record GMToolExecuteRequest(string ToolName, JsonElement Arguments, Guid GameId, Guid SessionId);
public record GMToolDeclineRequest(string? Reason);

public record PendingToolCallDto(
    Guid Id,
    Guid GameId,
    Guid SessionId,
    string ToolName,
    JsonElement Arguments,
    Guid? TargetPlayerId,
    DateTimeOffset StartedAt);

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
    IEventBus eventBus) : ControllerBase
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
        if (await RequireMemberAsync(gameId) is { } failure) return failure;

        var pending = await db.GMToolCalls
            .AsNoTracking()
            .Where(t => t.GameId == gameId && t.Status == GMToolCallStatus.AwaitingConfirmation)
            .OrderBy(t => t.StartedAt)
            .Select(t => new PendingToolCallDto(
                t.Id, t.GameId, t.SessionId, t.ToolName, t.Arguments, t.TargetPlayerId, t.StartedAt))
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

    [HttpPost("{id:guid}/confirm")]
    public Task<IActionResult> Confirm(Guid id, CancellationToken ct)
        => ResolveAsync(id, approved: true, reason: null, ct);

    [HttpPost("{id:guid}/decline")]
    public Task<IActionResult> Decline(Guid id, [FromBody] GMToolDeclineRequest? body, CancellationToken ct)
        => ResolveAsync(id, approved: false, reason: body?.Reason, ct);

    private async Task<IActionResult> ResolveAsync(Guid id, bool approved, string? reason, CancellationToken ct)
    {
        var toolCall = await db.GMToolCalls.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (toolCall is null)
            return NotFound(new { error = "Tool call not found." });

        if (await RequireMemberAsync(toolCall.GameId) is { } failure) return failure;

        if (toolCall.Status != GMToolCallStatus.AwaitingConfirmation)
            return BadRequest(new { error = "This tool call has already been resolved." });

        toolCall.Status = approved ? GMToolCallStatus.Completed : GMToolCallStatus.Declined;
        toolCall.CompletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        // Hands control back to ToolExecutionHandler so the saga can continue.
        if (toolCall.AgentCallId is { } agentCallId)
        {
            await eventBus.PublishAsync(new ToolCallConfirmationResolved(
                agentCallId, toolCall.GameId, toolCall.ToolName,
                toolCall.Arguments.GetRawText(), toolCall.ToolIndex, approved, reason), ct);
        }

        return NoContent();
    }

    /// <summary>Execute a GM tool by name.</summary>
    [HttpPost("execute")]
    public async Task<IActionResult> ExecuteTool([FromBody] GMToolExecuteRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ToolName))
            return BadRequest(new { error = "ToolName is required." });

        // Without this the endpoint ran GM tools against any caller-supplied game.
        if (await RequireCreatorAsync(request.GameId) is { } failure) return failure;

        if (gmToolRegistry.RequiresConfirmation(request.ToolName))
            return Accepted(new { message = "Awaiting confirmation", toolName = request.ToolName });

        try
        {
            var result = await gmToolRegistry.ExecuteToolAsync(
                request.ToolName, request.Arguments, request.GameId, request.SessionId, ct);

            return Ok(new { result });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private async Task<IActionResult?> RequireMemberAsync(Guid gameId)
    {
        try
        {
            await auth.RequirePlayerAsync(gameId, userIdProvider.GetUserId());
            return null;
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { error = ex.Message });
        }
    }

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
