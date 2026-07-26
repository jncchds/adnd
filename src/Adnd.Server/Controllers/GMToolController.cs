using System.Text.Json;
using Adnd.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Adnd.Server.Controllers;

public record GMToolExecuteRequest(string ToolName, JsonElement Arguments, Guid GameId, Guid SessionId);

[ApiController]
[Route("api/gmtools")]
[Authorize]
public class GMToolController(IGMToolRegistry gmToolRegistry) : ControllerBase
{
    /// <summary>List available GM tool definitions, optionally filtered by category.</summary>
    [HttpGet]
    public IActionResult ListTools([FromQuery] string? category)
    {
        var tools = gmToolRegistry.GetToolDefinitions();
        return Ok(tools);
    }

    /// <summary>Execute a GM tool by name.</summary>
    [HttpPost("execute")]
    public async Task<IActionResult> ExecuteTool(
        [FromBody] GMToolExecuteRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ToolName))
            return BadRequest("ToolName is required.");

        if (gmToolRegistry.RequiresConfirmation(request.ToolName))
            return Accepted(new { message = "Awaiting confirmation", toolName = request.ToolName });

        try
        {
            var result = await gmToolRegistry.ExecuteToolAsync(
                request.ToolName,
                request.Arguments,
                request.GameId,
                request.SessionId,
                ct);

            return Ok(new { result });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
