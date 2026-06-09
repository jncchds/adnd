using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Adnd.Server.Data;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Adnd.Server.Events;
using MediatR;
using System.Text.Json;
using System.Collections.Concurrent;

namespace Adnd.Server.Hubs;

public partial class GameHub : Hub
{
    // In-memory player-to-connection mapping (use Redis/DistributedCache in production)
    private static readonly ConcurrentDictionary<string, string> _playerConnections = new();

    private readonly AppDbContext _context;
    private readonly IGameEngine _gameEngine;
    private readonly IAgentBus _agentBus;
    private readonly IWhisperService _whisperService;
    private readonly ICombatService _combatService;
    private readonly IGMToolRegistry _toolRegistry;
    private readonly IMediator _mediator;
    private readonly IEmbeddingService _embeddingService;
    private readonly ILogger<GameHub> _logger;

    public GameHub(AppDbContext context, IGameEngine gameEngine, IAgentBus agentBus,
        IWhisperService whisperService, ICombatService combatService, IGMToolRegistry toolRegistry,
        IMediator mediator, IEmbeddingService embeddingService, ILogger<GameHub> logger)
    {
        _context = context;
        _gameEngine = gameEngine;
        _agentBus = agentBus;
        _whisperService = whisperService;
        _combatService = combatService;
        _toolRegistry = toolRegistry;
        _mediator = mediator;
        _embeddingService = embeddingService;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Generate an embedding for a message asynchronously.
    /// Called after messages are saved to the database.
    /// </summary>
    private async Task EmbedMessageAsync(Guid gameId, Guid messageId, string content)
    {
        try
        {
            var embedding = await _embeddingService.GenerateEmbeddingAsync(gameId, content);
            var message = await _context.Messages.FindAsync(messageId);
            if (message != null && message.Embedding == null)
            {
                message.Embedding = embedding;
                await _context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate embedding for message {MessageId} in game {GameId}", messageId, gameId);
        }
    }
}
