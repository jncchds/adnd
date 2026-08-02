using Adnd.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace Adnd.Server.Controllers;

// Every endpoint here drives LLM work billed to the game owner's API key and returns
// GM-only plot intelligence, so all of them are creator-scoped. None were checked at all.
[Route("api/plotweaver")]
public class PlotWeaverController(
    IPlotWeaver plotWeaver,
    IRAGService rag,
    IGameAuthorizationService auth,
    IUserIdProvider userIdProvider) : GameScopedController(auth, userIdProvider)
{
    [HttpPost("review/{gameId:guid}")]
    public async Task<IActionResult> Review(Guid gameId, CancellationToken ct)
    {
        if (await RequireCreator(gameId) is { } failure) return failure;

        await plotWeaver.ReviewAndAdaptAsync(gameId, ct);
        return Ok(new { message = "PlotWeaver review complete" });
    }

    [HttpGet("context/{gameId:guid}")]
    public async Task<IActionResult> GetContext(Guid gameId, CancellationToken ct)
    {
        if (await RequireCreator(gameId) is { } failure) return failure;

        var context = await rag.GeneratePlotContextAsync(gameId, ct);
        return Ok(new { context });
    }

    [HttpGet("consistency/{gameId:guid}")]
    public async Task<IActionResult> CheckConsistency(Guid gameId, CancellationToken ct)
    {
        if (await RequireCreator(gameId) is { } failure) return failure;

        var report = await rag.CheckPlotConsistencyAsync(gameId, ct);
        return Ok(report);
    }

    [HttpGet("continuation/{gameId:guid}")]
    public async Task<IActionResult> SuggestContinuation(Guid gameId, CancellationToken ct)
    {
        if (await RequireCreator(gameId) is { } failure) return failure;

        var continuation = await rag.SuggestContinuationAsync(gameId, ct);
        return Ok(continuation);
    }

    [HttpPost("embed/{gameId:guid}")]
    public async Task<IActionResult> EmbedMessages(Guid gameId, CancellationToken ct)
    {
        if (await RequireCreator(gameId) is { } failure) return failure;

        await rag.EmbedMessagesAsync(gameId, ct);
        return Ok(new { message = "Embedding batch complete" });
    }

    [HttpPost("session-summary/{gameId:guid}/{sessionId:guid}")]
    public async Task<IActionResult> SessionSummary(Guid gameId, Guid sessionId, CancellationToken ct)
    {
        if (await RequireCreator(gameId) is { } failure) return failure;

        var summary = await rag.GenerateSessionSummaryAsync(gameId, sessionId, ct);
        return Ok(new { summary });
    }
}
