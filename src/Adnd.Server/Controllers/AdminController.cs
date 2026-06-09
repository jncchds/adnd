using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Adnd.Server.Data;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Adnd.Server.Events;
using Adnd.Server.Hubs;
using MediatR;

namespace Adnd.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public partial class AdminController : ControllerBase
{
    protected readonly AppDbContext _context;
    protected readonly IGameEngine _gameEngine;
    protected readonly IRAGService _ragService;
    protected readonly IAgentBus _agentBus;
    protected readonly IWhisperService _whisperService;
    protected readonly ILLMPresetService _presetService;
    protected readonly ILLMInteractionLogger _interactionLogger;
    protected readonly IPlotWeaver _plotWeaver;
    protected readonly Adnd.Server.Services.IUserIdProvider _userIdProvider;
    protected readonly IGameAuthorizationService _authService;
    protected readonly IMediator _mediator;
    protected readonly IHubContext<GameHub> _hubContext;
    protected readonly ILogger<AdminController> _logger;
    protected readonly IGameStartService _gameStartService;

    // New services for quick-win features
    protected readonly ISessionNoteService _sessionNoteService;
    protected readonly IPromptTemplateService _promptTemplateService;
    protected readonly IDiceStatsService _diceStatsService;
    protected readonly IGameTemplateService _gameTemplateService;

    public AdminController(
        AppDbContext context,
        IGameEngine gameEngine,
        IRAGService ragService,
        IAgentBus agentBus,
        IWhisperService whisperService,
        ILLMPresetService presetService,
        ILLMInteractionLogger interactionLogger,
        IPlotWeaver plotWeaver,
        Adnd.Server.Services.IUserIdProvider userIdProvider,
        IGameAuthorizationService authService,
        IMediator mediator,
        IHubContext<GameHub> hubContext,
        ILogger<AdminController> logger,
        IGameStartService gameStartService,
        ISessionNoteService sessionNoteService,
        IPromptTemplateService promptTemplateService,
        IDiceStatsService diceStatsService,
        IGameTemplateService gameTemplateService)
    {
        _context = context;
        _gameEngine = gameEngine;
        _ragService = ragService;
        _agentBus = agentBus;
        _whisperService = whisperService;
        _presetService = presetService;
        _interactionLogger = interactionLogger;
        _plotWeaver = plotWeaver;
        _userIdProvider = userIdProvider;
        _authService = authService;
        _mediator = mediator;
        _hubContext = hubContext;
        _logger = logger;
        _gameStartService = gameStartService;
        _sessionNoteService = sessionNoteService;
        _promptTemplateService = promptTemplateService;
        _diceStatsService = diceStatsService;
        _gameTemplateService = gameTemplateService;
    }

    // ==================== Game Templates ====================

    /// <summary>
    /// Get all game templates for the current user.
    /// </summary>
    [HttpGet("game-templates")]
    public async Task<IActionResult> GetGameTemplates()
    {
        var userId = _userIdProvider.GetCurrentUserId();
        var templates = await _gameTemplateService.GetTemplatesAsync(userId);
        return Ok(templates);
    }

    /// <summary>
    /// Create a new game template from the current game configuration.
    /// </summary>
    [HttpPost("game-templates")]
    public async Task<IActionResult> CreateGameTemplate([FromBody] CreateGameTemplateRequest request)
    {
        var userId = _userIdProvider.GetCurrentUserId();
        var template = await _gameTemplateService.CreateTemplateAsync(
            userId,
            request.Name,
            request.DefaultName,
            request.SystemId,
            request.LLMPresetId,
            request.LLMPresetName,
            request.Language,
            request.PlotSeed,
            request.GameParameters);
        return Ok(template);
    }

    /// <summary>
    /// Update an existing game template.
    /// </summary>
    [HttpPut("game-templates/{id}")]
    public async Task<IActionResult> UpdateGameTemplate(Guid id, [FromBody] UpdateGameTemplateRequest request)
    {
        var userId = _userIdProvider.GetCurrentUserId();
        var template = await _gameTemplateService.UpdateTemplateAsync(
            userId, id,
            request.Name,
            request.DefaultName,
            request.SystemId,
            request.LLMPresetId,
            request.LLMPresetName,
            request.Language,
            request.PlotSeed,
            request.GameParameters);
        return Ok(template);
    }

    /// <summary>
    /// Delete a game template (owner only).
    /// </summary>
    [HttpDelete("game-templates/{id}")]
    public async Task<IActionResult> DeleteGameTemplate(Guid id)
    {
        var userId = _userIdProvider.GetCurrentUserId();
        try
        {
            await _gameTemplateService.DeleteTemplateAsync(userId, id);
            return Ok(new { message = "Template deleted." });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "Template not found." });
        }
    }

    /// <summary>
    /// Get a single game template by ID (owner only).
    /// </summary>
    [HttpGet("game-templates/{id}")]
    public async Task<IActionResult> GetGameTemplate(Guid id)
    {
        var userId = _userIdProvider.GetCurrentUserId();
        var template = await _gameTemplateService.GetTemplateAsync(userId, id);
        if (template == null)
            return NotFound(new { error = "Template not found." });
        return Ok(template);
    }
}

// ==================== Request DTOs ====================

public class CreateGameTemplateRequest
{
    public string Name { get; set; } = string.Empty;
    public string? DefaultName { get; set; }
    public string SystemId { get; set; } = "dnd5e";
    public Guid? LLMPresetId { get; set; }
    public string? LLMPresetName { get; set; }
    public string Language { get; set; } = "English";
    public string? PlotSeed { get; set; }
    public string? GameParameters { get; set; }
}

public class UpdateGameTemplateRequest
{
    public string Name { get; set; } = string.Empty;
    public string? DefaultName { get; set; }
    public string SystemId { get; set; } = "dnd5e";
    public Guid? LLMPresetId { get; set; }
    public string? LLMPresetName { get; set; }
    public string Language { get; set; } = "English";
    public string? PlotSeed { get; set; }
    public string? GameParameters { get; set; }
}
