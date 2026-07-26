namespace Adnd.Server.Models;

public class Game : ISoftDelete
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CreatorId { get; set; }
    public Guid? LLMPresetId { get; set; }
    public Guid? CurrentSessionId { get; set; }

    public string Name { get; set; } = string.Empty;
    public GameStatus Status { get; set; } = GameStatus.Draft;
    public GMStatus GMStatus { get; set; } = GMStatus.Idle;
    public string? LastGMAction { get; set; }
    public DateTimeOffset? LastGMActionAt { get; set; }

    public string SystemId { get; set; } = "dnd5e";
    public string? SystemVersion { get; set; }
    public string? CustomSystemJson { get; set; }
    public string Language { get; set; } = "English";
    public string? PlotSeed { get; set; }
    public string? GameParameters { get; set; }
    public string? GameState { get; set; }
    public string? InviteCode { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public User Creator { get; set; } = null!;
    public LLMPreset? LLMPreset { get; set; }
    public GameSession? CurrentSession { get; set; }

    public ICollection<Player> Players { get; set; } = [];
    public ICollection<GameSession> Sessions { get; set; } = [];
    public ICollection<NPC> NPCs { get; set; } = [];
    public ICollection<PlotThread> PlotThreads { get; set; } = [];
    public ICollection<PlotReview> PlotReviews { get; set; } = [];
    public ICollection<AgentCall> AgentCalls { get; set; } = [];
}
