namespace Adnd.Server.Models;

public class Game
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CreatorId { get; set; }
    public User? Creator { get; set; }
    public Guid? GameMasterId { get; set; }
    public User? GameMaster { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SystemId { get; set; } = "dnd5e"; // Default system
    public string? SystemVersion { get; set; }
    public string? CustomSystemJson { get; set; } // For custom systems
    public GameStatus Status { get; set; } = GameStatus.Draft;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public string? InviteCode { get; set; }

    // Plot seed: initial plot setup by Game Creator
    public string? PlotSeed { get; set; } // JSON: initial plot, tone, themes
    public string? GameParameters { get; set; } // JSON: difficulty, tone, pacing
    public string? GameState { get; set; } // JSON: current game state managed by GM/LLM

    public ICollection<Player> Players { get; set; } = new List<Player>();
    public ICollection<GameSession> Sessions { get; set; } = new List<GameSession>();
    public ICollection<NPC> NPCs { get; set; } = new List<NPC>();
    public ICollection<PlotThread> PlotThreads { get; set; } = new List<PlotThread>();
    public ICollection<AgentCall> AgentCalls { get; set; } = new List<AgentCall>();
}

public enum GameStatus
{
    Draft,
    Active,
    Archived,
    Finished
}

public enum GameMasterMode
{
    Manual,    // GM plays manually, LLM assists
    Assist,    // LLM assists GM with suggestions
    SemiAuto,  // LLM auto-generates narrative, GM reviews
    FullAuto   // LLM runs the game (GM oversees)
}
