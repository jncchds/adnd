namespace Adnd.Server.Models;

public class Game : ISoftDelete
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CreatorId { get; set; }
    public User? Creator { get; set; }
    // LLM preset used by all agents in this game (GM, RAG, NPC, etc.)
    public Guid? LLMPresetId { get; set; }
    public LLMPreset? LLMPreset { get; set; }

    // GM agent status
    public GMStatus GMStatus { get; set; } = GMStatus.Idle;
    public string? LastGMAction { get; set; }
    public DateTime? LastGMActionAt { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SystemId { get; set; } = "dnd5e"; // Default system
    public string? SystemVersion { get; set; }
    public string? CustomSystemJson { get; set; } // For custom systems
    public GameStatus Status { get; set; } = GameStatus.Draft;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public string? InviteCode { get; set; }

    // Game language — narration output language
    public string Language { get; set; } = "English";

    // Plot seed: initial plot setup by Game Creator
    public string? PlotSeed { get; set; } // JSON: initial plot, tone, themes
    public string? GameParameters { get; set; } // JSON: difficulty, tone, pacing
    public string? GameState { get; set; } // JSON: current game state managed by GM/LLM

    // Single session per game (auto-created)
    public Guid? CurrentSessionId { get; set; }
    public GameSession? CurrentSession { get; set; }

    // Soft-delete support
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }

    public ICollection<Player> Players { get; set; } = new List<Player>();
    public ICollection<GameSession> Sessions { get; set; } = new List<GameSession>();
    public ICollection<NPC> NPCs { get; set; } = new List<NPC>();
    public ICollection<PlotThread> PlotThreads { get; set; } = new List<PlotThread>();
    public ICollection<PlotReview> PlotReviews { get; set; } = new List<PlotReview>();
    public ICollection<AgentCall> AgentCalls { get; set; } = new List<AgentCall>();
}

public enum GameStatus
{
    Draft,
    Starting,
    Active,
    Ending,
    Archived,
    Finished
}

public enum GMStatus
{
    Idle,      // Game created, GM agent not yet activated
    Running,   // GM agent is actively running
    Paused     // GM agent paused (creator paused or game archived)
}
