using Microsoft.EntityFrameworkCore;
using Adnd.Server.Data;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Adnd.Server.Agent;

/// <summary>
/// Per-game game agent that processes events sequentially.
/// Events are persisted to the database (AgentCalls table) so they survive restarts.
/// </summary>
public class GameAgent : IGameAgent
{
    private readonly Guid _gameId;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IGameEngine _gameEngine;
    private readonly IRAGService _ragService;
    private readonly SystemRegistry _systemRegistry;
    private readonly ILogger<GameAgent> _logger;
    private readonly CancellationTokenSource _cts = new();
    private readonly ConcurrentQueue<Guid> _pendingCallIds = new();
    private readonly ManualResetEventSlim _signal = new(false);
    private Task? _processingLoop;
    private volatile bool _isPaused = false;

    /// <summary>
    /// Enqueue a pending call ID — called by AgentCallQueued event handler.
    /// The processing loop checks this queue before querying the database.
    /// </summary>
    public void EnqueueCall(Guid callId)
    {
        _pendingCallIds.Enqueue(callId);
        _signal.Set();
    }

    public GameAgent(
        Guid gameId,
        IServiceScopeFactory scopeFactory,
        IGameEngine gameEngine,
        IRAGService ragService,
        SystemRegistry systemRegistry,
        ILogger<GameAgent> logger)
    {
        _gameId = gameId;
        _scopeFactory = scopeFactory;
        _gameEngine = gameEngine;
        _ragService = ragService;
        _systemRegistry = systemRegistry;
        _logger = logger;
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
    /// Main processing loop — event-driven via _pendingCallIds queue.
    /// Blocks indefinitely on _signal.Wait() until a call is enqueued.
    /// No polling — if the agent crashes, pending calls in DB are recovered
    /// on next app start via GameAgentManager.StartAllActiveGamesAsync().
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
                    // Wait for a signal (new call arrived) — blocks indefinitely
                    // Use WaitHandle.WaitOne with timeout to support cancellation
                    bool signaled = _signal.Wait(Timeout.Infinite, _cts.Token);

                    // Use per-iteration scope to avoid DbContext lifetime issues
                    Game? game;
                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                        game = await context.Games.FindAsync(_gameId);
                    }

                    // Only process calls when the game is active/starting and the GM is running
                    if (game == null || (game.Status != Models.GameStatus.Active && game.Status != Models.GameStatus.Starting) || game.GMStatus == Models.GMStatus.Idle)
                    {
                        _signal.Reset();
                        continue;
                    }

                    if (_isPaused)
                    {
                        _signal.Reset();
                        continue;
                    }

                    // Drain the event-driven queue
                    List<AgentCall> pendingCalls = new();
                    while (_pendingCallIds.TryDequeue(out var callId))
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                        var call = await context.AgentCalls.FindAsync(callId);
                        if (call != null && call.Status == AgentCallStatus.Pending)
                        {
                            pendingCalls.Add(call);
                        }
                        else
                        {
                            // Call was already processed or doesn't exist — ignore
                            _logger.LogDebug("[AGENT] StaleCallInQueue | GameId={GameId} | CallId={CallId}", _gameId, callId);
                        }
                    }

                    if (pendingCalls.Any())
                    {
                        _logger.LogInformation("[AGENT] ProcessingBatch | GameId={GameId} | BatchSize={Size} | Sources={Sources}",
                            _gameId, pendingCalls.Count,
                            string.Join(", ", pendingCalls.Select(c => $"{c.FromAgent}->{c.Action}")));
                    }

                    // Process each pending event sequentially
                    foreach (var call in pendingCalls)
                    {
                        if (_cts.Token.IsCancellationRequested || _isPaused) break;

                        await ProcessCallAsync(call);
                    }

                    // Reset signal — it will be Set() again when new calls arrive
                    _signal.Reset();
                }
                catch (OperationCanceledException) when (_cts.Token.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in game agent processing loop for game {GameId}", _gameId);
                    _signal.Reset();
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
        _logger.LogInformation("[AGENT] CallStart | GameId={GameId} | CallId={CallId} | From={FromAgent} -> To={ToAgent} [{Action}] | CreatedAt={CreatedAt}",
            _gameId, call.Id, call.FromAgent, call.ToAgent, call.Action, call.CreatedAt);

        // Single scope for the entire call. AgentBus is Scoped and holds scoped deps
        // (AppDbContext, ILLMInteractionLogger). GameAgent is long-lived so we must
        // not hold scoped references beyond the scope that created them.
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        call.Status = AgentCallStatus.Running;
        call.StartedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        var agentBus = scope.ServiceProvider.GetRequiredService<IAgentBus>();

        try
        {
            var result = await agentBus.ExecuteCallAsync(call);

            call.Status = AgentCallStatus.Completed;
            call.CompletedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();

            _logger.LogInformation("[AGENT] CallComplete | GameId={GameId} | CallId={CallId} | Action={Action} | Duration={Duration}ms | OutputLen={OutputLen}",
                _gameId, call.Id, call.Action, call.DurationMs, call.Output?.Length ?? 0);
        }
        catch (Exception ex)
        {
            call.Status = AgentCallStatus.Failed;
            call.Error = ex.Message;
            call.CompletedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();

            _logger.LogError("[AGENT] CallFailed | GameId={GameId} | CallId={CallId} | Action={Action} | Duration={Duration}ms | Error={Error}",
                _gameId, call.Id, call.Action, call.DurationMs, ex.Message);
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        _signal.Dispose();
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
    private readonly SystemRegistry _systemRegistry;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ConcurrentDictionary<Guid, GameAgent> _agents = new();

    public GameAgentManager(
        IServiceScopeFactory scopeFactory,
        IGameEngine gameEngine,
        IRAGService ragService,
        SystemRegistry systemRegistry,
        ILoggerFactory loggerFactory)
    {
        _scopeFactory = scopeFactory;
        _gameEngine = gameEngine;
        _ragService = ragService;
        _systemRegistry = systemRegistry;
        _loggerFactory = loggerFactory;
    }

    /// <summary>
    /// Enqueue a pending call ID into the GameAgent's queue — called by the AgentCallQueued event handler.
    /// If the agent doesn't exist yet, the DB fallback in the loop will pick it up.
    /// </summary>
    public void OnAgentCallQueued(Guid gameId, Guid callId)
    {
        if (_agents.TryGetValue(gameId, out var agent))
        {
            try
            {
                agent.EnqueueCall(callId);
            }
            catch (Exception ex)
            {
                _loggerFactory.CreateLogger<GameAgentManager>()
                    .LogWarning(ex, "Failed to enqueue call for game {GameId}", gameId);
            }
        }
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
            var agent = new GameAgent(
                gameId,
                _scopeFactory,
                _gameEngine,
                _ragService,
                _systemRegistry,
                agentLogger);

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
