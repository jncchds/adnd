using Microsoft.EntityFrameworkCore;
using Adnd.Server.Data;
using Adnd.Server.Events;
using Adnd.Server.Handlers;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Microsoft.Extensions.Logging;
using Wolverine;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Adnd.Server.Agent;

/// <summary>
/// Per-game game agent that processes events reactively via Wolverine.
/// Saga state is persisted in AgentCall.CurrentStep and ToolCallCoordinator for crash recovery.
/// </summary>
public class GameAgent : IGameAgent
{
    private readonly Guid _gameId;
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GameAgent> _logger;
    private readonly IMessageContext _messageContext;
    private readonly CancellationTokenSource _cts = new();
    private volatile bool _isPaused = false;
    private volatile bool _isConnected = false;

    public GameAgent(
        Guid gameId,
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<GameAgent> logger,
        IMessageContext messageContext)
    {
        _gameId = gameId;
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
        _messageContext = messageContext;
    }

    public async Task StartAsync(Guid gameId, Guid creatorId)
    {
        if (_isConnected)
        {
            _logger.LogWarning("Game agent already connected for game {GameId}", gameId);
            return;
        }

        // Activate the game in the DB
        var game = await GetGameAsync(gameId);
        if (game == null)
            throw new KeyNotFoundException($"Game {gameId} not found.");

        if (game.LLMPresetId == null)
            throw new InvalidOperationException("Cannot start: no LLM preset configured.");

        game.Status = Models.GameStatus.Starting;
        game.StartedAt = DateTime.UtcNow;
        game.GMStatus = Models.GMStatus.Running;

        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await context.SaveChangesAsync();

        _logger.LogInformation("Starting game agent for game {GameId}", gameId);

        // Recover pending sagas
        await RecoverPendingSagas();

        _isConnected = true;
    }

    /// <summary>
    /// Dispatch an event to registered handlers. Called by the consumer on message receipt.
    /// Persists the event as an EventRecord and invokes all registered handlers.
    /// </summary>
    private async Task DispatchToHandlers(IGameEvent evt, string payload, IServiceProvider sp, CancellationToken ct)
    {
        using var scope = sp.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Persist as EventRecord
        var record = new EventRecord
        {
            Id = Guid.NewGuid(),
            GameId = evt.GameId,
            EventType = evt.GetType().FullName!,
            Payload = payload,
            Status = EventStatus.Published,
            CorrelationId = Guid.NewGuid().ToString(),
            CreatedAt = DateTime.UtcNow,
            PublishedAt = DateTime.UtcNow
        };
        context.EventRecords.Add(record);
        await context.SaveChangesAsync(ct);

        _logger.LogInformation("[CONSUMER] DispatchToHandlers | GameId={GameId} | EventType={EventType} | EventId={EventId}",
            evt.GameId, evt.GetType().Name, record.Id);

        // Dispatch to handlers
        var registry = scope.ServiceProvider.GetRequiredService<IHandlerRegistry>();
        var key = evt.GetType().FullName!;

        if (!registry.Handlers.TryGetValue(key, out var handlers))
        {
            _logger.LogDebug("[EVENT] NoHandlerForType | EventType={EventType} | EventId={EventId}",
                key, record.Id);
            record.Status = EventStatus.Acknowledged;
            record.AckedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(ct);
            return;
        }

        _logger.LogInformation("[EVENT] Found {Count} handlers for {EventType} | EventId={EventId}",
            handlers.Count, key, record.Id);

        var success = true;
        foreach (var handlerType in handlers)
        {
            try
            {
                _logger.LogInformation("[EVENT] Creating handler {Handler} for {EventType} | EventId={EventId}",
                    handlerType.Name, key, record.Id);

                using var handlerScope = sp.CreateScope();
                var handlerInstance = ActivatorUtilities.CreateInstance(handlerScope.ServiceProvider, handlerType);

                var handleMethod = handlerType.GetMethods()
                    .FirstOrDefault(m => m.Name == "HandleAsync"
                                         && m.GetParameters().Length >= 1
                                         && m.GetParameters()[0].ParameterType == evt.GetType());

                if (handleMethod == null)
                {
                    _logger.LogError("[EVENT] NoHandleAsync | Handler={HandlerType} | EventType={EventType}",
                        handlerType.Name, key);
                    success = false;
                    continue;
                }

                _logger.LogInformation("[EVENT] Invoking handler {Handler} | EventId={EventId}",
                    handlerType.Name, record.Id);

                var task = (Task)handleMethod.Invoke(handlerInstance, new object[] { evt, ct })!;
                await task;

                _logger.LogInformation("[EVENT] Handler {Handler} completed | EventId={EventId}",
                    handlerType.Name, record.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[EVENT] HandlerException | Handler={Handler} | EventType={EventType} | EventId={EventId}",
                    handlerType.Name, key, record.Id);
                success = false;
            }
        }

        record.Status = success ? EventStatus.Acknowledged : EventStatus.Failed;
        record.AckedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(ct);

        _logger.LogInformation("[EVENT] Dispatch complete | EventId={EventId} | Success={Success}",
            record.Id, success);
    }

    private async Task RecoverPendingSagas()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var pendingSagas = await context.AgentCalls
            .Where(c => c.GameId == _gameId &&
                       c.Status != AgentCallStatus.Completed &&
                       c.Status != AgentCallStatus.Failed &&
                       c.Status != AgentCallStatus.Cancelled)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();

        if (pendingSagas.Count == 0)
        {
            _logger.LogInformation("No pending sagas for game {GameId}", _gameId);
            return;
        }

        _logger.LogInformation("Recovering {Count} sagas for game {GameId}", pendingSagas.Count, _gameId);

        foreach (var call in pendingSagas)
        {
            // Check for coordinator
            var coordinator = await context.ToolCallCoordinators
                .FirstOrDefaultAsync(c => c.SagaId == call.Id && c.Status == CoordinatorStatus.Active);

            if (coordinator != null && coordinator.CompletedTools.Any())
            {
                // Resume from coordinator state
                var tools = JsonSerializer.Deserialize<List<ToolCallInfo>>(coordinator.ToolsJson) ?? new();
                if (coordinator.CurrentIndex < coordinator.TotalTools)
                {
                    var nextTool = tools[coordinator.CurrentIndex];
                    var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();
                    await eventBus.PublishAsync(new ToolCallRequested(
                        call.Id, call.GameId, coordinator.CurrentIndex, nextTool.Name, nextTool.Arguments));
                    _logger.LogInformation("Resumed saga {SagaId} from tool {Index}", call.Id, coordinator.CurrentIndex);
                }
                else if (coordinator.Status == CoordinatorStatus.Active)
                {
                    // All tools done but coordinator still active — emit follow-up
                    var toolResults = coordinator.CompletedTools
                        .OrderBy(t => t.Index)
                        .Select(t => new ToolResult(t.Index, t.ToolName, t.Result, t.Error))
                        .ToList();
                    var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();
                    await eventBus.PublishAsync(new LLMFollowUpRequested(call.Id, call.GameId, toolResults));
                    _logger.LogInformation("Resumed saga {SagaId} with follow-up LLM", call.Id);
                }
            }
            else
            {
                // No coordinator — emit AgentCallQueued to restart saga
                var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();
                await eventBus.PublishAsync(new AgentCallQueued(call.Id, call.GameId));
                _logger.LogInformation("Resumed saga {SagaId} from AgentCallQueued", call.Id);
            }
        }
    }

    public async Task PauseAsync(Guid gameId)
    {
        _isPaused = true;
        _logger.LogInformation("Pausing game agent for game {GameId}", gameId);

        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var game = await context.Games.FindAsync(gameId);
        if (game != null)
        {
            game.GMStatus = Models.GMStatus.Paused;
            game.LastGMAction = "Paused";
            game.LastGMActionAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
        }
    }

    public async Task ResumeAsync(Guid gameId)
    {
        _isPaused = false;
        _logger.LogInformation("Resuming game agent for game {GameId}", gameId);

        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var game = await context.Games.FindAsync(gameId);
        if (game != null)
        {
            game.GMStatus = Models.GMStatus.Running;
            game.LastGMAction = "Resumed";
            game.LastGMActionAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
        }
    }

    public async Task<GMStatus> GetStatusAsync(Guid gameId)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var game = await context.Games.FindAsync(gameId);
        if (game == null)
            throw new KeyNotFoundException($"Game {gameId} not found.");
        return game.GMStatus;
    }

    public bool IsActive(Guid gameId)
    {
        return _isConnected && !_isPaused;
    }

    public IEnumerable<Guid> GetActiveGameIds()
    {
        yield return _gameId;
    }

    private async Task<Game?> GetGameAsync(Guid gameId)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await context.Games.FindAsync(gameId);
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        _isConnected = false;
    }
}

/// <summary>
/// Manages all game agents — creates, tracks, and cleans up per-game agents.
/// Survives restarts by checking active games in the database on startup.
/// </summary>
public class GameAgentManager : IGameAgentManager, IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ConcurrentDictionary<Guid, GameAgent> _agents = new();

    public GameAgentManager(
        IServiceProvider serviceProvider,
        ILoggerFactory loggerFactory)
    {
        _serviceProvider = serviceProvider;
        _loggerFactory = loggerFactory;
    }

    public IGameAgent GetOrCreate(Guid gameId)
    {
        // Fast path: agent already exists and is active
        if (_agents.TryGetValue(gameId, out var existing))
        {
            if (existing.IsActive(gameId))
                return existing;

            // Agent was paused/stopped, recreate it
            _agents.TryRemove(gameId, out _);
        }

        // Use lock to prevent race condition: two concurrent calls can both create agents
        lock (_agents)
        {
            // Double-check after acquiring lock
            if (_agents.TryGetValue(gameId, out var existing2))
            {
                if (existing2.IsActive(gameId))
                    return existing2;
                _agents.TryRemove(gameId, out _);
            }

            var agentLogger = _loggerFactory.CreateLogger<GameAgent>();
            var config = _serviceProvider.GetRequiredService<IConfiguration>();
            var messageContext = _serviceProvider.GetRequiredService<IMessageContext>();
            var agent = new GameAgent(gameId, _serviceProvider, config, agentLogger, messageContext);

            if (_agents.TryAdd(gameId, agent))
            {
                var logger = _loggerFactory.CreateLogger<GameAgentManager>();
                logger.LogInformation("Created new game agent for game {GameId}", gameId);
            }

            return agent;
        }
    }

    public void Remove(Guid gameId)
    {
        if (_agents.TryRemove(gameId, out var agent))
        {
            agent.Dispose();
            var logger = _loggerFactory.CreateLogger<GameAgentManager>();
            logger.LogInformation("Removed game agent for game {GameId}", gameId);
        }
    }

    /// <summary>
    /// Called on startup to recover game agents from active games in the database.
    /// This ensures game agents survive container restarts.
    /// </summary>
    public async Task StartAllActiveGamesAsync()
    {
        var logger = _loggerFactory.CreateLogger<GameAgentManager>();
        logger.LogInformation("Recovering active game agents from database...");

        var activeGames = new List<Guid>();
        using (var scope = _serviceProvider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            activeGames = await context.Games
                .Where(g => g.Status == Models.GameStatus.Active && g.GMStatus == Models.GMStatus.Running)
                .Select(g => g.Id)
                .ToListAsync();
        }

        logger.LogInformation("Found {Count} active games to recover", activeGames.Count);

        foreach (var gameId in activeGames)
        {
            try
            {
                var agent = GetOrCreate(gameId);
                await agent.StartAsync(gameId, Guid.Empty);
                logger.LogInformation("Recovered game agent for game {GameId}", gameId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to recover game agent for game {GameId}", gameId);
            }
        }
    }

    public void Dispose()
    {
        foreach (var kvp in _agents)
        {
            kvp.Value.Dispose();
        }
        _agents.Clear();
    }
}
