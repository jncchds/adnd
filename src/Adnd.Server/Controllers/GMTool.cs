using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Adnd.Server.Services;
using Adnd.Server.Data;
using Adnd.Server.Models;

namespace Adnd.Server.Controllers;

/// <summary>
/// GM Tool Panel API.
/// Provides access to available GM tools and execution.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class GMToolController : ControllerBase
{
    private readonly IGMToolRegistry _toolRegistry;
    private readonly AppDbContext _context;
    private readonly IGameAuthorizationService _authService;
    private readonly Adnd.Server.Services.IUserIdProvider _userIdProvider;
    private readonly ILogger<GMToolController> _logger;

    public GMToolController(
        IGMToolRegistry toolRegistry,
        AppDbContext context,
        IGameAuthorizationService authService,
        Adnd.Server.Services.IUserIdProvider userIdProvider,
        ILogger<GMToolController> logger)
    {
        _toolRegistry = toolRegistry;
        _context = context;
        _authService = authService;
        _userIdProvider = userIdProvider;
        _logger = logger;
    }

    /// <summary>
    /// Get all available GM tools for a game.
    /// </summary>
    [HttpGet("games/{gameId}/tools")]
    public async Task<IActionResult> GetTools(Guid gameId)
    {
        var game = _context.Games.Find(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (!gameId.Equals(Guid.Empty) && !await _authService.HasAccessAsync(_context, gameId, _userIdProvider.GetCurrentUserId()))
            return Forbid();

        var tools = _toolRegistry.GetAvailableTools(gameId);

        return Ok(new
        {
            gameId,
            count = tools.Count,
            tools = tools.Select(t => new
            {
                t.Name,
                t.Description,
                t.Category,
                t.RequiresConfirmation,
                Parameters = t.Parameters
            })
        });
    }

    /// <summary>
    /// Get tools by category.
    /// </summary>
    [HttpGet("games/{gameId}/tools/{category}")]
    public async Task<IActionResult> GetToolsByCategory(Guid gameId, string category)
    {
        var game = _context.Games.Find(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (!gameId.Equals(Guid.Empty) && !await _authService.HasAccessAsync(_context, gameId, _userIdProvider.GetCurrentUserId()))
            return Forbid();

        var tools = _toolRegistry.GetToolsByCategory(gameId, Enum.Parse<ToolCategory>(category, true));

        return Ok(new
        {
            gameId,
            category,
            count = tools.Count,
            tools = tools.Select(t => new
            {
                t.Name,
                t.Description,
                t.Category,
                t.RequiresConfirmation,
                Parameters = t.Parameters
            })
        });
    }

    /// <summary>
    /// Execute a GM tool manually.
    /// </summary>
    [HttpPost("games/{gameId}/tools/execute")]
    public async Task<IActionResult> ExecuteTool(Guid gameId, Guid sessionId, [FromBody] ExecuteToolRequest request)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (!await _authService.HasAccessAsync(_context, gameId, _userIdProvider.GetCurrentUserId())) return Forbid();

        var result = await _toolRegistry.ExecuteToolAsync(gameId, sessionId, request.ToolName, request.Arguments);

        if (!result.Success)
            return BadRequest(new { error = result.Error, output = result.Output });

        return Ok(new
        {
            toolName = request.ToolName,
            success = result.Success,
            output = result.Output,
            outputMessage = result.OutputMessage,
            requiresUserInput = result.RequiresUserInput,
            userInputType = result.UserInputType
        });
    }
}

public class ExecuteToolRequest
{
    public string ToolName { get; set; } = string.Empty;
    public string Arguments { get; set; } = "{}";
}
