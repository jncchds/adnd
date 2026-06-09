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
    private readonly AppDbContext _context;
    private readonly IGameEngine _gameEngine;
    private readonly IRAGService _ragService;
    private readonly ILLMProviderRegistry _providerRegistry;
    private readonly IAgentBus _agentBus;
    private readonly IWhisperService _whisperService;
    private readonly ILLMPresetService _presetService;
    private readonly ILLMInteractionLogger _interactionLogger;
    private readonly IPlotWeaver _plotWeaver;
    private readonly Adnd.Server.Services.IUserIdProvider _userIdProvider;
    private readonly IGameAuthorizationService _authService;
    private readonly IMediator _mediator;
    private readonly IHubContext<GameHub> _hubContext;
    private readonly ILogger<AdminController> _logger;

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
        ILogger<AdminController> logger)
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
    }

}
