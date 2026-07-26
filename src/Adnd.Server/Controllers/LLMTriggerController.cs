using Adnd.Server.Models;
using Adnd.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Adnd.Server.Controllers;

public record LLMTriggerRequest(Guid GameId, Guid? SessionId, string? Context);

[ApiController]
[Route("api/llmtrigger")]
[Authorize]
public class LLMTriggerController(IAgentBus agentBus) : ControllerBase
{
    private async Task<IActionResult> QueueCall(LLMTriggerRequest req, AgentAction action)
    {
        var call = new AgentCall
        {
            GameId = req.GameId,
            SessionId = req.SessionId,
            FromAgent = AgentType.GM,
            ToAgent = AgentType.LLM,
            Action = action,
            Input = req.Context
        };
        var queued = await agentBus.SendCallAsync(call);
        return Accepted(new { agentCallId = queued.Id });
    }

    [HttpPost("narrate")]
    public Task<IActionResult> Narrate([FromBody] LLMTriggerRequest req) =>
        QueueCall(req, AgentAction.Narrate);

    [HttpPost("suggest")]
    public Task<IActionResult> Suggest([FromBody] LLMTriggerRequest req) =>
        QueueCall(req, AgentAction.Suggest);

    [HttpPost("consistency")]
    public Task<IActionResult> Consistency([FromBody] LLMTriggerRequest req) =>
        QueueCall(req, AgentAction.Check);

    [HttpPost("review")]
    public Task<IActionResult> Review([FromBody] LLMTriggerRequest req) =>
        QueueCall(req, AgentAction.Check);

    [HttpPost("new-scene")]
    public Task<IActionResult> NewScene([FromBody] LLMTriggerRequest req) =>
        QueueCall(req, AgentAction.Generate);

    [HttpPost("gm-evaluate")]
    public Task<IActionResult> GmEvaluate([FromBody] LLMTriggerRequest req) =>
        QueueCall(req, AgentAction.Query);

    [HttpPost("plot-check")]
    public Task<IActionResult> PlotCheck([FromBody] LLMTriggerRequest req) =>
        QueueCall(req, AgentAction.Check);

    [HttpPost("detect-opportunities")]
    public Task<IActionResult> DetectOpportunities([FromBody] LLMTriggerRequest req) =>
        QueueCall(req, AgentAction.Query);

    [HttpPost("generate-threads")]
    public Task<IActionResult> GenerateThreads([FromBody] LLMTriggerRequest req) =>
        QueueCall(req, AgentAction.GenerateInitialThreads);

    [HttpPost("spawn-milestones")]
    public Task<IActionResult> SpawnMilestones([FromBody] LLMTriggerRequest req) =>
        QueueCall(req, AgentAction.Generate);

    [HttpPost("session-summary")]
    public Task<IActionResult> SessionSummary([FromBody] LLMTriggerRequest req) =>
        QueueCall(req, AgentAction.Recall);
}
