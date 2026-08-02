using Wolverine.Persistence.Sagas;

namespace Adnd.Server.Events;

public record GameCreated(Guid GameId, string Name, Guid CreatorId) : IGameEvent;
public record GameStarted(Guid GameId) : IGameEvent;
public record GameArchived(Guid GameId) : IGameEvent;
public record GamePaused(Guid GameId) : IGameEvent;
public record GameResumed(Guid GameId) : IGameEvent;
public record GameNarrationStarted(Guid GameId, Guid AgentCallId) : IGameEvent;
public record GMStatusChanged(Guid GameId, string Status) : IGameEvent;
public record GameStatusChanged(Guid GameId, string Status) : IGameEvent;

public record PlayerJoined(Guid GameId, Guid PlayerId, Guid UserId, string CharacterName) : IGameEvent;
public record PlayerLeft(Guid GameId, Guid PlayerId, string Reason = "") : IGameEvent;
public record PlayerDisconnected(Guid GameId, Guid PlayerId) : IGameEvent;
public record PlayerReconnected(Guid GameId, Guid PlayerId) : IGameEvent;
public record PlayerRoleChanged(Guid GameId, Guid PlayerId, string NewRole) : IGameEvent;

public record SessionCreated(Guid GameId, Guid SessionId) : IGameEvent;
public record SessionClosed(Guid GameId, Guid SessionId) : IGameEvent;

public record MessageSent(Guid GameId, Guid SessionId, Guid? PlayerId, string Content, string MessageType) : IGameEvent;
public record WhisperSent(Guid GameId, Guid SessionId, Guid? FromPlayerId, string Content) : IGameEvent;
public record OOCMessageSent(Guid GameId, Guid SessionId, Guid? PlayerId, string Content) : IGameEvent;
public record OOCWhisperSent(Guid GameId, Guid SessionId, Guid? FromPlayerId, string Content) : IGameEvent;
public record OOCWhisperReceived(Guid GameId, Guid SessionId, Guid? ToPlayerId, string Content) : IGameEvent;

public record DiceRolled(Guid GameId, Guid? PlayerId, string Formula, int Total, string Breakdown) : IGameEvent;
public record SkillCheckRequested(Guid GameId, Guid? PlayerId, string SkillId, int DC) : IGameEvent;
public record AttackRequested(Guid GameId, Guid? PlayerId, Guid? TargetId, int AttackBonus) : IGameEvent;

public record CombatStarted(Guid GameId, Guid CombatId, string Name) : IGameEvent;
public record CombatEnded(Guid GameId, Guid CombatId, List<Guid> DefeatedNPCIds) : IGameEvent;
public record ParticipantAdded(Guid GameId, Guid CombatId, Guid ParticipantId, string DisplayName) : IGameEvent;
public record ParticipantRemoved(Guid GameId, Guid CombatId, Guid ParticipantId) : IGameEvent;
public record InitiativeRolled(Guid GameId, Guid CombatId, Guid ParticipantId, float Initiative) : IGameEvent;
public record InitiativeRolledForAll(Guid GameId, Guid CombatId) : IGameEvent;
public record TurnAdvanced(Guid GameId, Guid CombatId, int Round, int TurnIndex) : IGameEvent;
public record TurnRetreated(Guid GameId, Guid CombatId) : IGameEvent;
public record CombatAttackExecuted(Guid GameId, Guid CombatId, Guid AttackerId, Guid TargetId, int Roll, int Damage) : IGameEvent;
public record CombatSaveThrowExecuted(Guid GameId, Guid CombatId, Guid ParticipantId, string SaveType, int Roll, int DC, bool Success) : IGameEvent;
public record CombatSpellCast(Guid GameId, Guid CombatId, Guid CasterId, string SpellName, int SpellLevel) : IGameEvent;
public record CombatConditionApplied(Guid GameId, Guid CombatId, Guid ParticipantId, string Condition) : IGameEvent;
public record CombatConditionRemoved(Guid GameId, Guid CombatId, Guid ParticipantId, string Condition) : IGameEvent;
public record CombatDamageDealt(Guid GameId, Guid CombatId, Guid TargetId, int Amount, string DamageType) : IGameEvent;
public record CombatHealed(Guid GameId, Guid CombatId, Guid TargetId, int Amount) : IGameEvent;
public record CombatXPGranted(Guid GameId, Guid CombatId, int XP) : IGameEvent;
public record CombatLevelUp(Guid GameId, Guid CharacterId, int NewLevel) : IGameEvent;
public record CombatRestStarted(Guid GameId, string RestType) : IGameEvent;
public record CombatRestEnded(Guid GameId, string RestType) : IGameEvent;
public record CombatGridSet(Guid GameId, Guid CombatId, int Width, int Height) : IGameEvent;
public record CombatPositionSet(Guid GameId, Guid CombatId, Guid ParticipantId, int X, int Y) : IGameEvent;
public record CombatMove(Guid GameId, Guid CombatId, Guid ParticipantId, int X, int Y) : IGameEvent;

public record CharacterUpdated(Guid GameId, Guid CharacterId) : IGameEvent;
public record NPCCreated(Guid GameId, Guid NPCId, string Name) : IGameEvent;
public record NPCUpdated(Guid GameId, Guid NPCId) : IGameEvent;
public record NPCDeleted(Guid GameId, Guid NPCId) : IGameEvent;
public record PlotThreadCreated(Guid GameId, Guid ThreadId, string Title) : IGameEvent;
public record PlotThreadUpdated(Guid GameId, Guid ThreadId) : IGameEvent;

public record StorySwayed(Guid GameId, string Direction, int Intensity, string Content) : IGameEvent;
public record GMActioned(Guid GameId, Guid AgentCallId, string Action) : IGameEvent;

// AgentCall pipeline. Messages routed to AgentSaga must carry [SagaIdentity] on
// AgentCallId — Wolverine otherwise only recognises a property named Id/SagaId/AgentSagaId
// and cannot correlate the message back to its saga.
public record AgentCallQueued([property: SagaIdentity] Guid AgentCallId, Guid GameId);
public record LLMDispatchRequested(Guid AgentCallId, Guid GameId, string SystemPrompt, string UserPrompt);
public record LLMResponseReceived([property: SagaIdentity] Guid AgentCallId, Guid GameId, string ResponseText, bool HasToolCalls, int ToolCount, string? RawJson);
public record ToolCallRequested(Guid AgentCallId, Guid GameId, string ToolName, string ArgumentsJson, int ToolIndex);
public record ToolCallCompleted([property: SagaIdentity] Guid AgentCallId, Guid GameId, string ToolName, string ResultJson, int ToolIndex, int TotalTools);
public record LLMFollowUpRequested(Guid AgentCallId, Guid GameId, string SystemPrompt, string UserPrompt, string ToolResultsSummary);
public record NarrativeReady([property: SagaIdentity] Guid AgentCallId, Guid GameId, Guid SessionId, string NarrativeText);

/// <summary>A step failed. Not terminal — the DLQ handler decides whether to retry.</summary>
public record AgentCallFailed(Guid AgentCallId, Guid GameId, string Error);

/// <summary>Retries are exhausted. Terminal; the saga owns this transition.</summary>
public record AgentCallAbandoned([property: SagaIdentity] Guid AgentCallId, Guid GameId, string Error);
public record ToolCallWaitingConfirmation(Guid AgentCallId, Guid GameId, string ToolName, string ArgumentsJson, Guid? TargetPlayerId);
public record ToolCallConfirmationResolved(Guid AgentCallId, Guid GameId, string ToolName, string ArgumentsJson, int ToolIndex, bool Approved, string? DeclineReason);

/// <summary>
/// A player has answered the reroll offer that followed a requestPlayerRoll. <paramref
/// name="FeatureId"/> is null when they kept the original roll, in which case the interim
/// result stands and the turn simply resumes.
/// </summary>
public record RerollResolved(Guid AgentCallId, Guid GameId, string ArgumentsJson, int ToolIndex, Guid CharacterId, string? FeatureId, string InterimContent, Guid? PromptMessageId);
