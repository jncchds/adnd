using Adnd.Server.Models;
using Adnd.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Adnd.Server.Controllers;

[ApiController]
[Route("api/plotweaver")]
[Authorize]
public class PlotWeaverController(IAgentBus agentBus) : ControllerBase
{
    /// <summary>
    /// Triggers a PlotWeaver review for the specified game.
    /// Queues an AgentCall — full IPlotWeaver service is implemented in Phase 7.
    /// </summary>
    [HttpPost("review/{gameId:guid}")]
    public async Task<IActionResult> Review(Guid gameId, CancellationToken ct)
    {
        var call = new AgentCall
        {
            GameId = gameId,
            FromAgent = AgentType.GM,
            ToAgent = AgentType.System,
            Action = AgentAction.Check,
            Input = "plot-weaver-review"
        };

        var queued = await agentBus.SendCallAsync(call);
        return Accepted(new { agentCallId = queued.Id, message = "PlotWeaver review queued" });
    }
}
