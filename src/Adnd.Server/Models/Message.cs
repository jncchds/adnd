using System.Text.Json;
using System.ComponentModel.DataAnnotations.Schema;
using Pgvector;

namespace Adnd.Server.Models;

public class Message : ISoftDelete
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public GameSession? Session { get; set; }
    public Guid? PlayerId { get; set; }
    public Player? Player { get; set; }
    public string Content { get; set; } = string.Empty;
    public MessageType Type { get; set; } = MessageType.InGamePublic;
    public JsonElement Metadata { get; set; } // Dice results, skill checks, etc.
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // OOC flag: OOC messages don't influence game narrative
    public bool IsOOC { get; set; } = false;

    // Whisper fields (only populated for whisper-type messages)
    public Guid? WhisperFromId { get; set; } // Player who sent the whisper
    public Player? WhisperFrom { get; set; }
    public Guid? WhisperToId { get; set; } // Single target player ID
    public Player? WhisperTo { get; set; }
    public string? WhisperTarget { get; set; } // "all", "player:{userId}", "group:{groupName}"

    // PGVector embedding for semantic search
    public Vector? Embedding { get; set; }

    // Soft-delete support
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
}

public enum MessageType
{
    // === In-game messages (influence narrative) ===
    InGamePublic = 0,      // Public chat visible to all — part of game narrative
    InGameWhisper = 1,     // Whisper to GM — adds to GM knowledge, answered in-character

    // === OOC messages (never influence narrative) ===
    OOCPublic = 2,         // Out-of-character public chat
    OOCWhisper = 3,        // Out-of-character whisper to GM (for clarification)

    // === Game action messages ===
    Action = 4,            // Player action (attack, skill check, etc.)
    Dice = 5,              // Dice roll result
    SkillCheck = 10,       // Skill check result
    Attack = 11,           // Attack result
    SpellCast = 12,        // Spell cast result

    // === Combat messages ===
    CombatStart = 20,      // Combat started
    CombatEnd = 21,        // Combat ended
    CombatPause = 22,      // Combat paused
    CombatResume = 23,     // Combat resumed
    Initiative = 24,       // Initiative roll
    InitiativeComplete = 25, // Initiative complete (turn order)
    TurnAdvanced = 26,     // Turn advanced
    TurnRetreated = 27,    // Turn retreated
    TurnSet = 28,          // Turn set to specific participant
    DamageDealt = 29,      // Damage dealt
    DamageTaken = 30,      // Damage taken (mirror)
    Healed = 31,           // Healing applied
    DeathSave = 32,        // Death save
    ConditionApplied = 33, // Condition applied
    ConditionRemoved = 34, // Condition removed
    XPGranted = 35,        // XP granted
    LevelUp = 36,          // Level up
    SANLoss = 37,          // SAN loss (CoC)
    SANRecovery = 38,      // SAN recovery (CoC)
    SANCheck = 39,         // SAN check (CoC)

    // === Combat action economy ===
    ActionSpent = 40,      // Action spent
    BonusActionSpent = 41, // Bonus action spent
    ReactionSpent = 42,    // Reaction spent
    MovementSpent = 43,    // Movement spent
    ActionsRefreshed = 44, // Actions refreshed

    // === Combat state changes ===
    ParticipantAdded = 50, // Participant added to combat
    ParticipantRemoved = 51, // Participant removed from combat
    GridSet = 52,          // Combat grid set
    PositionSet = 53,      // Participant position set
    CombatMove = 54,       // Participant moved
    ItemAdded = 55,        // Item added to inventory
    ItemRemoved = 56,      // Item removed from inventory
    ItemEquipped = 57,     // Item equipped
    ItemUnequipped = 58,   // Item unequipped

    // === Player lifecycle ===
    PlayerJoined = 60,     // Player joined game
    PlayerLeft = 61,       // Player left game
    PlayerDisconnected = 62, // Player disconnected
    PlayerReconnected = 63,  // Player reconnected
    PlayerRoleChanged = 64,  // Player role changed

    // === Character lifecycle ===
    CharacterCreated = 70, // Character created
    CharacterUpdated = 71, // Character updated

    // === Session lifecycle ===
    SessionCreated = 80,   // Session created
    SessionClosed = 81,    // Session closed
    GameStarted = 82,      // Game started
    GamePaused = 83,       // Game paused
    GameResumed = 84,      // Game resumed
    GameArchived = 85,     // Game archived

    // === GM / AI messages ===
    GM = 7,                // GM narrative message
    Narration = 86,        // AI-GM narration
    Suggestion = 87,       // AI-GM suggestion
    ConsistencyCheck = 88, // Consistency check result
    PlotReview = 89,       // Plot review result
    PlotThreadCreated = 90, // Plot thread created
    PlotThreadUpdated = 91, // Plot thread updated
    NPCEvent = 92,         // NPC creation/update/deletion

    // === System / meta messages ===
    System = 6,            // System notification
    AgentCall = 8,         // Agent framework call log
    AgentResponse = 9,     // Agent framework response
    ToolCall = 93,         // GM tool call (narration, query, etc.)
    ToolCallConfirmed = 94, // Tool call confirmed
    ToolCallDenied = 95,   // Tool call denied
    PlayerRollRequest = 96, // Player roll requested by GM
    PlayerRollConfirmed = 97, // Player roll confirmed
    PlayerRollDeclined = 98, // Player roll declined
    PlayerRollResult = 99, // Player roll result
    StateChange = 100,     // Game state change
    AICombatSuggestion = 101, // AI combat suggestion
    AICombatAutoResolve = 102, // AI combat auto-resolve
}
