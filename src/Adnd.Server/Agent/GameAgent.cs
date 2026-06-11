using Microsoft.EntityFrameworkCore;
using Adnd.Server.Data;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Adnd.Server.Events;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json;
using MediatR;

namespace Adnd.Server.Agent;

/// <summary>
/// Per-game game agent that processes events sequentially.
/// Events are persisted to the database (AgentCalls table) so they survive restarts.
/// </summary>
public class GameAgent : IGameAgent
{
    private readonly Guid _gameId;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IAgentBus _agentBus;
    private readonly IGameEngine _gameEngine;
    private readonly IRAGService _ragService;
    private readonly ISystemRegistry _systemRegistry;
    private readonly ILogger<GameAgent> _logger;
    private readonly IMediator _mediator;
    private readonly CancellationTokenSource _cts = new();
    private TaskCompletionSource<bool> _wakeupTcs;
    private Task? _processingLoop;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private volatile bool _isPaused = false;

    /// <summary>
    /// Wake up the processing loop immediately (used by MediatR event subscriber).
    /// The loop waits on _wakeupTcs when idle — resetting it signals the loop to check for pending calls.
    /// </summary>
    public void WakeUp()
    {
        // Reset the TCS so the loop can wait on it again next idle cycle
        // Create a new TCS since the old one is already completed
        _wakeupTcs.TrySetResult(true);
        _wakeupTcs = new TaskCompletionSource<bool>();
    }

    public GameAgent(
        Guid gameId,
        IServiceScopeFactory scopeFactory,
        IAgentBus agentBus,
        IGameEngine gameEngine,
        IRAGService ragService,
        ISystemRegistry systemRegistry,
        ILogger<GameAgent> logger,
        IMediator mediator)
    {
        _gameId = gameId;
        _scopeFactory = scopeFactory;
        _agentBus = agentBus;
        _gameEngine = gameEngine;
        _ragService = ragService;
        _systemRegistry = systemRegistry;
        _logger = logger;
        _mediator = mediator;
        _wakeupTcs = new TaskCompletionSource<bool>();
    }

    private async Task WithContextAsync(Func<AppDbContext, Task> action)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await action(context);
    }

    private async Task<T> WithContextAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await action(context);
    }

    public async Task StartAsync(Guid gameId, Guid creatorId)
    {
        if (_processingLoop != null && !_processingLoop.IsCompleted)
        {
            _logger.LogWarning("Game agent already running for game {GameId}", gameId);
            return;
        }

        // Activate the game in the DB
        Game? game;
        using (var scope = _scopeFactory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            game = await context.Games.FindAsync(gameId);
        }
        if (game == null)
            throw new KeyNotFoundException($"Game {gameId} not found.");

        if (game.LLMPresetId == null)
            throw new InvalidOperationException("Cannot start: no LLM preset configured.");

        using (var scope = _scopeFactory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            game = await context.Games.FindAsync(gameId);
            game.Status = Models.GameStatus.Starting;
            game.StartedAt = DateTime.UtcNow;
            game.GMStatus = Models.GMStatus.Running;
            await context.SaveChangesAsync();
        }

        _logger.LogInformation("Starting game agent for game {GameId}", gameId);
        _processingLoop = ProcessLoopAsync();
    }

    public async Task PauseAsync(Guid gameId)
    {
        _isPaused = true;
        _logger.LogInformation("Pausing game agent for game {GameId}", gameId);

        using (var scope = _scopeFactory.CreateScope())
        {
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
    }

    public async Task ResumeAsync(Guid gameId)
    {
        _isPaused = false;
        _logger.LogInformation("Resuming game agent for game {GameId}", gameId);

        using (var scope = _scopeFactory.CreateScope())
        {
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

        // Restart the processing loop if it was disposed (e.g., due to error or unexpected exit)
        if (_processingLoop == null || _processingLoop.IsCompleted)
        {
            var loopState = _processingLoop == null ? "null" : _processingLoop.Status.ToString();
            _logger.LogInformation("Restarting processing loop for game {GameId} (loop was {State})",
                gameId, loopState);
            _processingLoop = ProcessLoopAsync();
        }
    }

    public async Task QueueEventAsync(Guid gameId, AgentCall call)
    {
        call.GameId = gameId;
        call.Status = AgentCallStatus.Pending;
        call.CreatedAt = DateTime.UtcNow;

        using (var scope = _scopeFactory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            context.AgentCalls.Add(call);
            await context.SaveChangesAsync();
        }

        _logger.LogDebug("Event queued for game {GameId}: {FromAgent} → {ToAgent} [{Action}]",
            gameId, call.FromAgent, call.ToAgent, call.Action);

        // Wake up the GameAgent immediately — eliminates polling delay
        // Note: we can't access _mediator here directly, so the caller (AgentBus) handles publishing
    }

    public async Task<GMStatus> GetStatusAsync(Guid gameId)
    {
        Game? game;
        using (var scope = _scopeFactory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            game = await context.Games.FindAsync(gameId);
        }
        if (game == null)
            throw new KeyNotFoundException($"Game {gameId} not found.");

        return game.GMStatus;
    }

    public bool IsActive(Guid gameId)
    {
        return _processingLoop != null && !_processingLoop.IsCompleted && !_isPaused;
    }

    public IEnumerable<Guid> GetActiveGameIds()
    {
        yield return _gameId;
    }

    /// <summary>
    /// Main processing loop — picks up pending events from the DB and processes them.
    /// Survives restarts because it polls the database for pending events.
    /// Uses MediatR event (AgentCallQueued) to wake up immediately when calls are queued,
    /// eliminating the 1s polling delay while keeping crash recovery via polling.
    /// </summary>
    private async Task ProcessLoopAsync()
    {
        _logger.LogInformation("Game agent processing loop started for game {GameId}", _gameId);

        try
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    // Use per-iteration scope to avoid DbContext lifetime issues
                    // (single scope for long-running loop causes stale data and ObjectDisposedException)
                    Game? game;
                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                        game = await context.Games.FindAsync(_gameId);
                    }
                    if (game == null || game.Status != Models.GameStatus.Active || game.GMStatus == Models.GMStatus.Idle)
                    {
                        await Task.Delay(5000, _cts.Token);
                        continue;
                    }

                    if (_isPaused)
                    {
                        await Task.Delay(5000, _cts.Token);
                        continue;
                    }

                    // Get pending events for this game
                    List<AgentCall> pendingCalls;
                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                        pendingCalls = await context.AgentCalls
                            .Where(c => c.GameId == _gameId && c.Status == AgentCallStatus.Pending)
                            .OrderBy(c => c.CreatedAt)
                            .Take(10)
                            .ToListAsync(_cts.Token);
                    }

                    if (pendingCalls.Count == 0)
                    {
                        // Wait for wakeup signal (from AgentCallQueued event) or timeout (crash recovery)
                        try
                        {
                            await Task.WhenAny(
                                _wakeupTcs.Task,
                                Task.Delay(1000, _cts.Token));
                            // Reset for next idle cycle
                            _wakeupTcs = new TaskCompletionSource<bool>();
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                        continue;
                    }

                    // Process each pending event sequentially
                    foreach (var call in pendingCalls)
                    {
                        if (_cts.Token.IsCancellationRequested || _isPaused) break;

                        await ProcessCallAsync(call);
                    }

                    // Auto-narrate removed: idle time = players thinking or between sessions.
                    // PlotWeaver handles dynamic story development via event-driven momentum tracking.
                }
                catch (OperationCanceledException) when (_cts.Token.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in game agent processing loop for game {GameId}", _gameId);
                    await Task.Delay(5000, _cts.Token);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }

        _logger.LogInformation("Game agent processing loop stopped for game {GameId}", _gameId);
    }

    private async Task ProcessCallAsync(AgentCall call)
    {
        using (var scope = _scopeFactory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            call.Status = AgentCallStatus.Running;
            call.StartedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
        }

        try
        {
            var result = await _agentBus.ExecuteCallAsync(call);

            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                call.Status = AgentCallStatus.Completed;
                call.CompletedAt = DateTime.UtcNow;
                await context.SaveChangesAsync();
            }

            _logger.LogDebug("Event processed for game {GameId}: {Action} (call: {CallId})",
                _gameId, call.Action, call.Id);
        }
        catch (Exception ex)
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                call.Status = AgentCallStatus.Failed;
                call.Error = ex.Message;
                call.CompletedAt = DateTime.UtcNow;
                await context.SaveChangesAsync();
            }

            _logger.LogError(ex, "Error processing event for game {GameId}: {Action} (call: {CallId})",
                _gameId, call.Action, call.Id);
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        _semaphore.Dispose();
    }
}

/// <summary>
/// Manages all game agents — creates, tracks, and cleans up per-game agents.
/// Survives restarts by checking active games in the database on startup.
/// </summary>
public class GameAgentManager : IGameAgentManager, IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IGameEngine _gameEngine;
    private readonly IRAGService _ragService;
    private readonly ISystemRegistry _systemRegistry;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ConcurrentDictionary<Guid, GameAgent> _agents = new();

    public GameAgentManager(
        IServiceScopeFactory scopeFactory,
        IGameEngine gameEngine,
        IRAGService ragService,
        ISystemRegistry systemRegistry,
        ILoggerFactory loggerFactory)
    {
        _scopeFactory = scopeFactory;
        _gameEngine = gameEngine;
        _ragService = ragService;
        _systemRegistry = systemRegistry;
        _loggerFactory = loggerFactory;
    }

    /// <summary>
    /// Wake up a GameAgent when a new call is queued — eliminates polling delay.
    /// Called by the AgentCallQueued event handler.
    /// </summary>
    public void OnAgentCallQueued(Guid gameId, Guid callId)
    {
        if (_agents.TryGetValue(gameId, out var agent))
        {
            try
            {
                agent.WakeUp();
            }
            catch (Exception ex)
            {
                _loggerFactory.CreateLogger<GameAgentManager>()
                    .LogWarning(ex, "Failed to wake up game agent for game {GameId}", gameId);
            }
        }
        // If agent doesn't exist yet, the polling loop will pick it up within 1s
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
            // Resolve IAgentBus and IMediator within a scope to avoid singleton-scoped DbContext sharing
            using var scope = _scopeFactory.CreateScope();
            var agentBus = scope.ServiceProvider.GetRequiredService<IAgentBus>();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var agent = new GameAgent(
                gameId,
                _scopeFactory,
                agentBus,
                _gameEngine,
                _ragService,
                _systemRegistry,
                agentLogger,
                mediator);

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
        using (var scope = _scopeFactory.CreateScope())
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
                // Start the processing loop — without this, recovered agents are dead (never start polling)
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
