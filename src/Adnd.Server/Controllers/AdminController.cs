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
    protected readonly ILLMProviderRegistry _providerRegistry;
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

    public AdminController(
        AppDbContext context,
        IGameEngine gameEngine,
        IRAGService ragService,
        ILLMProviderRegistry providerRegistry,
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
        IDiceStatsService diceStatsService)
    {
        _context = context;
        _gameEngine = gameEngine;
        _ragService = ragService;
        _providerRegistry = providerRegistry;
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
    }
}
