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
    /// the original roll. Either way the GM's turn — which has been held open precisely so it
    /// never narrates a number the player is about to replace — resumes here.
    /// </summary>
    [HttpPost("{id:guid}/reroll")]
    public async Task<IActionResult> Reroll(Guid id, [FromBody] RerollRequest? body, CancellationToken ct)
    {
        var toolCall = await db.GMToolCalls.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (toolCall is null)
            return NotFound(new { error = "Tool call not found." });

        if (toolCall.Status != GMToolCallStatus.AwaitingReroll)
            return BadRequest(new { error = "This roll is not awaiting a reroll." });

        // The offer belongs to one player. Authorizing only on game membership would let any
        // player at the table spend someone else's Lucky points.
        var userId = userIdProvider.GetUserId();
        Player caller;
        try
        {
            caller = await auth.RequirePlayerAsync(toolCall.GameId, userId);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { error = ex.Message });
        }

        if (toolCall.TargetPlayerId is { } target && target != caller.Id)
            return StatusCode(403, new { error = "This reroll was offered to another player." });

        var featureId = string.IsNullOrWhiteSpace(body?.FeatureId) ? null : body!.FeatureId;

        // Only an ability that was actually offered may be taken — otherwise the client could
        // name any id and the roll would be reattempted under an ability the trigger never fired for.
        if (featureId is not null && !WasOffered(toolCall.Result, featureId))
            return BadRequest(new { error = "That ability was not offered for this roll." });

        toolCall.Status = GMToolCallStatus.Completed;
        toolCall.CompletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        if (toolCall.AgentCallId is { } agentCallId && toolCall.RerollCharacterId is { } characterId)
        {
            await eventBus.PublishAsync(new RerollResolved(
                agentCallId, toolCall.GameId, toolCall.Arguments.GetRawText(), toolCall.ToolIndex,
                characterId, featureId, ReadInterimContent(toolCall.Result), toolCall.PromptMessageId), ct);
        }

        return NoContent();
    }

    private static bool WasOffered(JsonElement result, string featureId)
        => result.ValueKind == JsonValueKind.Object
           && result.TryGetProperty("options", out var options)
           && options.ValueKind == JsonValueKind.Array
           && options.EnumerateArray().Any(o =>
               o.TryGetProperty("featureId", out var id)
               && string.Equals(id.GetString(), featureId, StringComparison.OrdinalIgnoreCase));

    private static string ReadInterimContent(JsonElement result)
        => result.ValueKind == JsonValueKind.Object
           && result.TryGetProperty("content", out var content)
           && content.ValueKind == JsonValueKind.String
            ? content.GetString() ?? string.Empty
            : string.Empty;

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

        if (gmToolRegistry.RequiresConfirmation(request.ToolName, request.Arguments))
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
