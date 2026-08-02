using System.Text.Json;
using Adnd.Server.Data;
using Adnd.Server.Dtos;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Controllers;

public record LLMTriggerRequest(Guid GameId, Guid? SessionId, string? Context);

[Route("api/llmtrigger")]
public class LLMTriggerController(
    IAgentBus agentBus,
    AppDbContext db,
    IGameAuthorizationService auth,
    IUserIdProvider userIdProvider) : GameScopedController(auth, userIdProvider)
{
    private async Task<IActionResult> QueueCall(LLMTriggerRequest req, AgentAction action)
    {
        // Every one of these queues a billable LLM call. Without a membership check any
        // authenticated user could spend another user's API budget and inject narration
        // into their game.
        if (await RequireCreator(req.GameId) is { } failure) return failure;

        var game = await db.Games.FirstOrDefaultAsync(g => g.Id == req.GameId);
        var systemPrompt = $"You are the AI Game Master. Perform the '{action}' action for this session.";
        if (game?.LanguageDirective is { } languageDirective)
            systemPrompt = $"{systemPrompt} {languageDirective}";

        // Input must be serialized GMDispatchOptions — the dispatch handler deserializes
        // it, and a bare context string would abort the call as malformed.
        var options = new GMDispatchOptions(
            SystemPrompt: systemPrompt,
            UserPrompt: req.Context ?? $"Perform the {action} action.");

        var call = new AgentCall
        {
            GameId = req.GameId,
            SessionId = req.SessionId,
            FromAgent = AgentType.GM,
            ToAgent = AgentType.LLM,
            Action = action,
            Input = JsonSerializer.Serialize(options)
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
