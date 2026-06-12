using System.Security.Cryptography;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Adnd.Server.Data;
using Adnd.Server.Models;
using Adnd.Server.Hubs;

namespace Adnd.Server.Services;

/// <summary>
/// Strategy interface for generating opening narrative text.
/// Each LLM provider type may have its own narrative generation approach.
/// </summary>
public interface INarrativeGenerationStrategy
{
    /// <summary>
    /// Generates an opening narrative for a game session.
    /// </summary>
    Task<string> GenerateOpeningNarrative(Game game);
}

/// <summary>
/// Factory for narrative generation strategies based on LLM provider type.
/// </summary>
public interface INarrativeGenerationFactory
{
    INarrativeGenerationStrategy GetStrategy(string providerType);
    void RegisterStrategy(INarrativeGenerationStrategy strategy);
}

/// <summary>
/// Service that orchestrates the game start process using strategy pattern.
/// Extracts game start logic from AdminController into a separate, testable service.
/// </summary>
public interface IGameStartService
{
    Task<StartGameResult> StartGameAsync(Guid gameId, Guid userId);
}

/// <summary>
/// Result of a game start operation.
/// </summary>
public class StartGameResult
{
    public Guid GameId { get; init; }
    public GameStatus Status { get; init; }
    public DateTime? StartedAt { get; init; }
    public string? OpeningNarrative { get; init; }
    public int PlotThreadsGenerated { get; init; }
    public bool Success { get; init; }
    public string? Error { get; init; }
}

// ============================================================================
// Narrative Generation Strategies
// ============================================================================

/// <summary>
/// Default narrative generation using the primary LLM provider.
/// </summary>
public class DefaultNarrativeGenerator : INarrativeGenerationStrategy
{
    private readonly ILLMProviderFactory _providerFactory;
    private readonly IApiKeyEncryptionService _encryption;

    public DefaultNarrativeGenerator(
        ILLMProviderFactory providerFactory,
        IApiKeyEncryptionService encryption)
    {
        _providerFactory = providerFactory;
        _encryption = encryption;
    }

    private ILLMProvider? ResolveProvider(LLMPreset preset)
    {
        // Decrypt the API key if needed
        if (preset.ApiKey != null && preset.DecryptedApiKey == null)
        {
            try
            {
                preset.DecryptedApiKey = _encryption.Decrypt(preset.ApiKey);
            }
            catch (CryptographicException)
            {
                return null;
            }
        }

        return _providerFactory.CreateFromPreset(preset);
    }

    public async Task<string> GenerateOpeningNarrative(Game game)
    {
        var llp = game.LLMPreset;
        if (llp == null)
        {
            return "The adventure begins...";
        }

        var provider = ResolveProvider(llp);
        if (provider == null)
        {
            return "The adventure begins...";
        }

        var systemPrompt = $"You are the Game Master for a TTRPG session. " +
            $"Game system: {game.SystemId}. " +
            $"Plot seed: {game.PlotSeed ?? "None"}. " +
            $"Game parameters: {game.GameParameters ?? "None"}. " +
            $"Your task is to write an immersive opening scene that introduces the world, " +
            $"sets the tone, and invites the players into the story. Be vivid and engaging. " +
            $"Write in second person to immerse the players. Limit to 2-4 paragraphs.";

        var userPrompt = "Generate the opening narrative for this game session. " +
            "Write it as if you are describing the scene to the players at the table.";

        var result = await provider.CompleteAsync(systemPrompt, userPrompt, new LLMOptions
        {
            Temperature = 0.85f,
            MaxTokens = 2048
        });

        return result.Trim();
    }
}

/// <summary>
/// Quick narrative generation for systems without LLM providers.
/// Uses predefined templates based on the game system.
/// </summary>
public class TemplateNarrativeGenerator : INarrativeGenerationStrategy
{

    public Task<string> GenerateOpeningNarrative(Game game)
    {
        var templates = new Dictionary<string, string>
        {
            ["dnd5e"] = "The sun rises over a land steeped in mystery and danger. Ancient ruins dot the horizon, their stone walls whispering forgotten secrets. A figure approaches from the mist — a traveler with a proposition that could change everything. Where will your journey begin?",
            ["pf2e"] = "The world of Golarion stretches before you, vast and teeming with possibility. From the frostbitten peaks of the World's Edge Mountains to the sun-drenched jungles of Cheliax, adventure awaits. A mysterious artifact has been discovered, and word has reached your ears. The call to adventure cannot be ignored.",
            ["coc7e"] = "The year is 1920. The world is on the brink of change, but some truths are better left unknown. A letter arrives at your door, sealed with wax and bearing a handwriting you don't recognize. It speaks of things that should not be — things that lurk in the shadows between the stars. You feel a chill that has nothing to do with the weather."
        };

        var template = templates.GetValueOrDefault(game.SystemId,
            "The adventure begins... The world awaits your heroes. What story will you tell?");

        return Task.FromResult(template);
    }
}

/// <summary>
/// Factory implementation for narrative generation strategies.
/// </summary>
public class NarrativeGenerationFactory : INarrativeGenerationFactory
{
    private readonly ILogger<NarrativeGenerationFactory> _logger;
    private readonly Dictionary<string, INarrativeGenerationStrategy> _strategies = new();

    public NarrativeGenerationFactory(
        ILLMProviderFactory providerFactory,
        IApiKeyEncryptionService encryption,
        ILogger<NarrativeGenerationFactory> logger)
    {
        _logger = logger;

        // Register default generators (pass factory + encryption for runtime provider resolution)
        RegisterStrategy(new DefaultNarrativeGenerator(providerFactory, encryption));
        RegisterStrategy(new TemplateNarrativeGenerator());
    }

    public void RegisterStrategy(string key, INarrativeGenerationStrategy strategy)
    {
        _strategies[key] = strategy;
    }

    public INarrativeGenerationStrategy GetStrategy(string providerType)
    {
        // If the provider type matches a registered strategy, use it
        if (_strategies.TryGetValue(providerType, out var strategy))
            return strategy;

        // All providers are created from presets at runtime via the factory
        return _strategies["default"];
    }

    public void RegisterStrategy(INarrativeGenerationStrategy strategy)
    {
        // Use the provider type as key for DefaultNarrativeGenerator, "default" or "template" for others
        var key = strategy is DefaultNarrativeGenerator ? "default" :
                  strategy is TemplateNarrativeGenerator ? "template" : "unknown";
        _strategies[key] = strategy;
    }
}

/// <summary>
/// Service that orchestrates game start using the Strategy pattern.
/// Each step is a composable action that can be tested independently.
/// </summary>
public class GameStartService : IGameStartService
{
    private readonly AppDbContext _context;
    private readonly IPlotWeaver _plotWeaver;
    private readonly INarrativeGenerationFactory _narrativeFactory;
    private readonly IHubContext<GameHub> _hubContext;
    private readonly IEventBus _eventBus;
    private readonly IEmbeddingService _embeddingService;
    private readonly ILogger<GameStartService> _logger;
    private readonly ILLMProviderFactory _providerFactory;
    private readonly IApiKeyEncryptionService _encryption;

    public GameStartService(
        AppDbContext context,
        IPlotWeaver plotWeaver,
        INarrativeGenerationFactory narrativeFactory,
        IHubContext<GameHub> hubContext,
        IEventBus mediator,
        IEmbeddingService embeddingService,
        ILogger<GameStartService> logger,
        ILLMProviderFactory providerFactory,
        IApiKeyEncryptionService encryption)
    {
        _context = context;
        _plotWeaver = plotWeaver;
        _narrativeFactory = narrativeFactory;
        _hubContext = hubContext;
        _eventBus = mediator;
        _embeddingService = embeddingService;
        _logger = logger;
        _providerFactory = providerFactory;
        _encryption = encryption;
    }

    public async Task<StartGameResult> StartGameAsync(Guid gameId, Guid userId)
    {
        var game = await _context.Games
            .Include(g => g.LLMPreset)
            .FirstOrDefaultAsync(g => g.Id == gameId);

        if (game == null)
            return new StartGameResult { GameId = gameId, Success = false, Error = "Game not found." };

        if (game.CreatorId != userId)
            return new StartGameResult { GameId = gameId, Success = false, Error = "Forbidden." };

        if (game.LLMPresetId == null)
            return new StartGameResult { GameId = gameId, Success = false, Error = "No LLM preset configured." };

        // Step 1: Update game status
        game.Status = GameStatus.Active;
        game.StartedAt = DateTime.UtcNow;
        game.GMStatus = GMStatus.Running;
        await _context.SaveChangesAsync();

        // Step 2: Publish game started event → triggers GameAgent activation
        // The GameAgent handles: initial plot thread generation (via PlotWeaverHandler)
        // and opening narrative generation (via OpenNarrative AgentCall).
        await _eventBus.PublishAsync(new Events.GameStarted(gameId, userId));

        return new StartGameResult
        {
            GameId = gameId,
            Status = game.Status,
            StartedAt = game.StartedAt,
            OpeningNarrative = null,
            PlotThreadsGenerated = 0,
            Success = true
        };
    }
}
