using MediatR;

namespace Adnd.Server.Events;

// ==================== Game Lifecycle Events ====================

public record GameCreated(Guid GameId, Guid CreatorId, string SystemId, Guid? LLMPresetId) : INotification;

public record GameStarted(Guid GameId, Guid CreatorId) : INotification;

public record GameArchived(Guid GameId) : INotification;

public record GamePaused(Guid GameId) : INotification;

public record GameResumed(Guid GameId) : INotification;

public record GameNarrationStarted(Guid GameId, Guid MessageId) : INotification;

public record GMStatusChanged(Guid GameId, Models.GMStatus NewStatus, string? LastAction) : INotification;

public record GameStatusChanged(Guid GameId, Models.GameStatus NewStatus) : INotification;

public record InitialThreadsGenerated(Guid GameId, int ThreadCount) : INotification;

// ==================== Player Events ====================

public record PlayerJoined(Guid GameId, Guid PlayerId, Guid UserId, string CharacterName) : INotification;

public record PlayerLeft(Guid GameId, Guid PlayerId) : INotification;

public record PlayerRoleChanged(Guid GameId, Guid PlayerId, string NewRole) : INotification;

// ==================== Session Events ====================

public record SessionCreated(Guid GameId, Guid SessionId, string Title) : INotification;

public record SessionClosed(Guid GameId, Guid SessionId) : INotification;

// ==================== Chat Events ====================

public record MessageSent(Guid GameId, Guid SessionId, Guid PlayerId, string Content, MessageType Type, string? Metadata, bool IsOOC = false) : INotification;

public record WhisperSent(Guid GameId, Guid FromPlayerId, string Targets, string Content, WhisperType Type) : INotification;

// OOC-specific events — never processed by game agent
public record OOCMessageSent(Guid GameId, Guid SessionId, Guid PlayerId, string Content, string OOCChannel) : INotification;
public record OOCWhisperSent(Guid GameId, Guid FromPlayerId, string Targets, string Content) : INotification;
public record OOCWhisperReceived(Guid GameId, Guid ToPlayerId, Guid FromPlayerId, string Content) : INotification;

// ==================== Game Action Events ====================

public record DiceRolled(Guid GameId, Guid SessionId, string Formula, Guid? PlayerId) : INotification;

public record SkillCheckRequested(Guid GameId, Guid SessionId, string Skill, Guid? PlayerId, int? DC) : INotification;

public record AttackRequested(Guid GameId, Guid SessionId, string Weapon, string Target, Guid? PlayerId) : INotification;

public record CombatStarted(Guid GameId, Guid? SessionId, string? Name) : INotification;

public record CombatEnded(Guid GameId, Guid CombatId, string? Result) : INotification;

public record ParticipantAdded(Guid GameId, Guid CombatId, string ParticipantType, string DisplayName, int AC, int CurrentHP, int MaxHP, Guid? PlayerId, Guid? NPCId) : INotification;

public record ParticipantRemoved(Guid GameId, Guid CombatId, Guid ParticipantId) : INotification;

public record InitiativeRolled(Guid GameId, Guid CombatId, Guid ParticipantId, string Formula) : INotification;

public record InitiativeRolledForAll(Guid GameId, Guid CombatId, string Formula) : INotification;

public record TurnAdvanced(Guid GameId, Guid CombatId) : INotification;

public record TurnRetreated(Guid GameId, Guid CombatId) : INotification;

public record CombatAttackExecuted(Guid GameId, Guid CombatId, string Attacker, string Weapon, Guid Target, string AttackFormula, string? DamageFormula) : INotification;

public record CombatSaveThrowExecuted(Guid GameId, Guid CombatId, string Participant, Guid ParticipantId, string SaveType, string SaveFormula, int DC) : INotification;

public record CombatSpellCast(Guid GameId, Guid CombatId, string Caster, string SpellName, Guid Target, int SaveDC, string? DamageFormula) : INotification;

public record CombatConditionApplied(Guid GameId, Guid CombatId, Guid ParticipantId, string ConditionName, int? Duration) : INotification;

public record CombatConditionRemoved(Guid GameId, Guid CombatId, Guid ParticipantId, string ConditionName) : INotification;

public record CombatDamageDealt(Guid GameId, Guid CombatId, Guid ParticipantId, int Damage, string? Source) : INotification;

public record CombatHealed(Guid GameId, Guid CombatId, Guid ParticipantId, int Amount, string? Source) : INotification;

public record CombatXPGranted(Guid GameId, Guid CombatId, Guid ParticipantId, int XP, string Reason) : INotification;

public record CombatLevelUp(Guid GameId, Guid CombatId, Guid ParticipantId, int NewLevel, string SystemId) : INotification;

public record CombatRestStarted(Guid GameId, Guid CombatId, string RestType) : INotification;

public record CombatRestEnded(Guid GameId, Guid CombatId) : INotification;

public record CombatGridSet(Guid GameId, Guid CombatId, int Width, int Height) : INotification;

public record CombatPositionSet(Guid GameId, Guid CombatId, Guid ParticipantId, int GridX, int GridY) : INotification;

public record CombatMove(Guid GameId, Guid CombatId, Guid ParticipantId, int GridX, int GridY) : INotification;

// ==================== Character Events ====================

public record CharacterUpdated(Guid GameId, Guid CharacterId) : INotification;

// ==================== Plot/NPC Events ====================

public record NPCCreated(Guid GameId, Guid NPCId, string Name) : INotification;

public record NPCUpdated(Guid GameId, Guid NPCId) : INotification;

public record NPCDeleted(Guid GameId, Guid NPCId) : INotification;

public record PlotThreadCreated(Guid GameId, Guid ThreadId, string Title) : INotification;

public record PlotThreadUpdated(Guid GameId, Guid ThreadId) : INotification;

// ==================== Sway Events ====================

public record StorySwayed(Guid GameId, Guid CreatorId, string Direction) : INotification;

public record GMActioned(Guid GameId, string Action, string? OutputMessage, string? Error) : INotification;

// ==================== Agent Call Events ====================

public record AgentCallQueued(Guid GameId, Guid CallId) : INotification;

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
