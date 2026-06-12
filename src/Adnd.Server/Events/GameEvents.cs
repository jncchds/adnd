

namespace Adnd.Server.Events;

// ==================== Game Lifecycle Events ====================

public record GameCreated(Guid GameId, Guid CreatorId, string SystemId, Guid? LLMPresetId) : IGameEvent;

public record GameStarted(Guid GameId, Guid CreatorId) : IGameEvent;

public record GameArchived(Guid GameId) : IGameEvent;

public record GamePaused(Guid GameId) : IGameEvent;

public record GameResumed(Guid GameId) : IGameEvent;

public record GameNarrationStarted(Guid GameId, Guid MessageId) : IGameEvent;

public record GMStatusChanged(Guid GameId, Models.GMStatus NewStatus, string? LastAction) : IGameEvent;

public record GameStatusChanged(Guid GameId, Models.GameStatus NewStatus) : IGameEvent;

public record InitialThreadsGenerated(Guid GameId, int ThreadCount) : IGameEvent;

// ==================== Player Events ====================

public record PlayerJoined(Guid GameId, Guid PlayerId, Guid UserId, string CharacterName) : IGameEvent;

public record PlayerLeft(Guid GameId, Guid PlayerId) : IGameEvent;

public record PlayerRoleChanged(Guid GameId, Guid PlayerId, string NewRole) : IGameEvent;

// ==================== Session Events ====================

public record SessionCreated(Guid GameId, Guid SessionId, string Title) : IGameEvent;

public record SessionClosed(Guid GameId, Guid SessionId) : IGameEvent;

// ==================== Chat Events ====================

public record MessageSent(Guid GameId, Guid SessionId, Guid PlayerId, string Content, MessageType Type, string? Metadata, bool IsOOC = false) : IGameEvent;

public record WhisperSent(Guid GameId, Guid FromPlayerId, string Targets, string Content, WhisperType Type) : IGameEvent;

// OOC-specific events — never processed by game agent
public record OOCMessageSent(Guid GameId, Guid SessionId, Guid PlayerId, string Content, string OOCChannel) : IGameEvent;
public record OOCWhisperSent(Guid GameId, Guid FromPlayerId, string Targets, string Content) : IGameEvent;
public record OOCWhisperReceived(Guid GameId, Guid ToPlayerId, Guid FromPlayerId, string Content) : IGameEvent;

// ==================== Game Action Events ====================

public record DiceRolled(Guid GameId, Guid SessionId, string Formula, Guid? PlayerId) : IGameEvent;

public record SkillCheckRequested(Guid GameId, Guid SessionId, string Skill, Guid? PlayerId, int? DC) : IGameEvent;

public record AttackRequested(Guid GameId, Guid SessionId, string Weapon, string Target, Guid? PlayerId) : IGameEvent;

public record CombatStarted(Guid GameId, Guid? SessionId, string? Name) : IGameEvent;

public record CombatEnded(Guid GameId, Guid CombatId, string? Result) : IGameEvent;

public record ParticipantAdded(Guid GameId, Guid CombatId, string ParticipantType, string DisplayName, int AC, int CurrentHP, int MaxHP, Guid? PlayerId, Guid? NPCId) : IGameEvent;

public record ParticipantRemoved(Guid GameId, Guid CombatId, Guid ParticipantId) : IGameEvent;

public record InitiativeRolled(Guid GameId, Guid CombatId, Guid ParticipantId, string Formula) : IGameEvent;

public record InitiativeRolledForAll(Guid GameId, Guid CombatId, string Formula) : IGameEvent;

public record TurnAdvanced(Guid GameId, Guid CombatId) : IGameEvent;

public record TurnRetreated(Guid GameId, Guid CombatId) : IGameEvent;

public record CombatAttackExecuted(Guid GameId, Guid CombatId, string Attacker, string Weapon, Guid Target, string AttackFormula, string? DamageFormula) : IGameEvent;

public record CombatSaveThrowExecuted(Guid GameId, Guid CombatId, string Participant, Guid ParticipantId, string SaveType, string SaveFormula, int DC) : IGameEvent;

public record CombatSpellCast(Guid GameId, Guid CombatId, string Caster, string SpellName, Guid Target, int SaveDC, string? DamageFormula) : IGameEvent;

public record CombatConditionApplied(Guid GameId, Guid CombatId, Guid ParticipantId, string ConditionName, int? Duration) : IGameEvent;

public record CombatConditionRemoved(Guid GameId, Guid CombatId, Guid ParticipantId, string ConditionName) : IGameEvent;

public record CombatDamageDealt(Guid GameId, Guid CombatId, Guid ParticipantId, int Damage, string? Source) : IGameEvent;

public record CombatHealed(Guid GameId, Guid CombatId, Guid ParticipantId, int Amount, string? Source) : IGameEvent;

public record CombatXPGranted(Guid GameId, Guid CombatId, Guid ParticipantId, int XP, string Reason) : IGameEvent;

public record CombatLevelUp(Guid GameId, Guid CombatId, Guid ParticipantId, int NewLevel, string SystemId) : IGameEvent;

public record CombatRestStarted(Guid GameId, Guid CombatId, string RestType) : IGameEvent;

public record CombatRestEnded(Guid GameId, Guid CombatId) : IGameEvent;

public record CombatGridSet(Guid GameId, Guid CombatId, int Width, int Height) : IGameEvent;

public record CombatPositionSet(Guid GameId, Guid CombatId, Guid ParticipantId, int GridX, int GridY) : IGameEvent;

public record CombatMove(Guid GameId, Guid CombatId, Guid ParticipantId, int GridX, int GridY) : IGameEvent;

// ==================== Character Events ====================

public record CharacterUpdated(Guid GameId, Guid CharacterId) : IGameEvent;

// ==================== Plot/NPC Events ====================

public record NPCCreated(Guid GameId, Guid NPCId, string Name) : IGameEvent;

public record NPCUpdated(Guid GameId, Guid NPCId) : IGameEvent;

public record NPCDeleted(Guid GameId, Guid NPCId) : IGameEvent;

public record PlotThreadCreated(Guid GameId, Guid ThreadId, string Title) : IGameEvent;

public record PlotThreadUpdated(Guid GameId, Guid ThreadId) : IGameEvent;

// ==================== Sway Events ====================

public record StorySwayed(Guid GameId, Guid CreatorId, string Direction) : IGameEvent;

public record GMActioned(Guid GameId, string Action, string? OutputMessage, string? Error) : IGameEvent;

// ==================== Agent Call Events ====================

public record AgentCallQueued(Guid GameId, Guid CallId) : IGameEvent;

// ==================== Helper Enums (inline to avoid duplicate definitions) ====================

public enum MessageType
{
    // === In-game messages (influence narrative) ===
    InGamePublic = 0,
    InGameWhisper = 1,

    // === OOC messages (never influence narrative) ===
    OOCPublic = 2,
    OOCWhisper = 3,

    // === System / meta messages ===
    Action = 4,
    Dice = 5,
    System = 6,
    GM = 7,
    AgentCall = 8,
    AgentResponse = 9
}

public enum WhisperType
{
    // === In-game whispers (narrative) ===
    InGamePlayerToGM = 0,
    InGameGMToPlayer = 1,

    // === OOC whispers (non-narrative) ===
    OOCPlayerToGM = 2,
    OOCGMToPlayer = 3,

    // === Legacy (kept for compatibility) ===
    PlayerToPlayer = 4,
    GMToGroup = 5,
    GMToAll = 6,
}
