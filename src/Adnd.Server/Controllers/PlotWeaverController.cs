using Adnd.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Adnd.Server.Controllers;

[ApiController]
[Route("api/plotweaver")]
[Authorize]
public class PlotWeaverController(
    IPlotWeaver plotWeaver,
    IRAGService rag) : ControllerBase
{
    [HttpPost("review/{gameId:guid}")]
    public async Task<IActionResult> Review(Guid gameId, CancellationToken ct)
    {
        await plotWeaver.ReviewAndAdaptAsync(gameId, ct);
        return Ok(new { message = "PlotWeaver review complete" });
    }

    [HttpGet("context/{gameId:guid}")]
    public async Task<IActionResult> GetContext(Guid gameId, CancellationToken ct)
    {
        var context = await rag.GeneratePlotContextAsync(gameId, ct);
        return Ok(new { context });
    }

    [HttpGet("consistency/{gameId:guid}")]
    public async Task<IActionResult> CheckConsistency(Guid gameId, CancellationToken ct)
    {
        var report = await rag.CheckPlotConsistencyAsync(gameId, ct);
        return Ok(report);
    }

    [HttpGet("continuation/{gameId:guid}")]
    public async Task<IActionResult> SuggestContinuation(Guid gameId, CancellationToken ct)
    {
        var continuation = await rag.SuggestContinuationAsync(gameId, ct);
        return Ok(continuation);
    }

    [HttpPost("embed/{gameId:guid}")]
    public async Task<IActionResult> EmbedMessages(Guid gameId, CancellationToken ct)
    {
        await rag.EmbedMessagesAsync(gameId, ct);
        return Ok(new { message = "Embedding batch complete" });
    }

    [HttpPost("session-summary/{gameId:guid}/{sessionId:guid}")]
    public async Task<IActionResult> SessionSummary(Guid gameId, Guid sessionId, CancellationToken ct)
    {
        var summary = await rag.GenerateSessionSummaryAsync(gameId, sessionId, ct);
        return Ok(new { summary });
    }
}
