using Adnd.Server.Data;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Controllers;

public record CreateAgentCallRequest(
    Guid GameId,
    Guid? SessionId,
    AgentType FromAgent,
    AgentType ToAgent,
    AgentAction Action,
    string? Input);

[ApiController]
[Route("api/agentcalls")]
[Authorize]
public class AgentFrameworkController(AppDbContext db, IAgentBus agentBus) : ControllerBase
{
    /// <summary>List agent calls for a game with optional status filter.</summary>
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] Guid gameId,
        [FromQuery] AgentCallStatus? status,
        CancellationToken ct)
    {
        var query = db.AgentCalls.Where(a => a.GameId == gameId);

        if (status.HasValue)
            query = query.Where(a => a.Status == status.Value);

        var calls = await query
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);

        return Ok(calls);
    }

    /// <summary>Get a single agent call by ID.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var call = await db.AgentCalls
            .Include(a => a.ChildCalls)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (call == null) return NotFound();
        return Ok(call);
    }

    /// <summary>List pending agent calls for a game.</summary>
    [HttpGet("pending")]
    public async Task<IActionResult> Pending([FromQuery] Guid gameId, CancellationToken ct)
    {
        var calls = await db.AgentCalls
            .Where(a => a.GameId == gameId && a.Status == AgentCallStatus.Pending)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(ct);

        return Ok(calls);
    }

    /// <summary>Create and queue a new agent call.</summary>
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateAgentCallRequest request,
        CancellationToken ct)
    {
        var call = new AgentCall
        {
            GameId = request.GameId,
            SessionId = request.SessionId,
            FromAgent = request.FromAgent,
            ToAgent = request.ToAgent,
            Action = request.Action,
            Input = request.Input
        };

        var queued = await agentBus.SendCallAsync(call);
        return Accepted(queued);
    }
}
