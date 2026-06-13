using Adnd.Server.Models;

namespace Adnd.Server.Hubs;

// ==================== Response Types ====================

public class ToolCallConfirmationResponse
{
    public Guid Id { get; set; }
    public string ToolName { get; set; } = string.Empty;
    public bool Approved { get; set; }
    public string? OutputMessage { get; set; }
    public ToolCallStatus Status { get; set; }
}

public class PlayerRollConfirmationResponse
{
    public Guid ToolCallId { get; set; }
    public bool Approved { get; set; }
    public string Skill { get; set; } = string.Empty;
    public string Formula { get; set; } = string.Empty;
    public int DC { get; set; }
    public string Context { get; set; } = string.Empty;
    public bool Optional { get; set; }
}

public class ToolCallInfo
{
    public Guid Id { get; set; }
    public string ToolName { get; set; } = string.Empty;
    public ToolCallStatus Status { get; set; }
    public string? Arguments { get; set; }
    public string? OutputMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool RequiresConfirmation { get; set; }
}

// ============= Response DTOs =============

public class WhisperResponse
{
    public Guid Id { get; set; }
    public Guid FromPlayerId { get; set; }
    public string FromCharacter { get; set; } = string.Empty;
    public PlayerRole FromRole { get; set; }
    public string Content { get; set; } = string.Empty;
    public Adnd.Server.Models.WhisperType Type { get; set; }
    public string Targets { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class AgentCallResponse
{
    public Guid Id { get; set; }
    public AgentType FromAgent { get; set; }
    public AgentType ToAgent { get; set; }
    public AgentAction Action { get; set; }
    public AgentCallStatus Status { get; set; }
    public string? Output { get; set; }
    public string? OutputMessage { get; set; }
    public int DurationMs { get; set; }
    public string? Error { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

// ============= Combat Response DTOs =============

public class CombatLogResponse
{
    public Guid CombatId { get; set; }
    public string? Name { get; set; }
    public string Status { get; set; } = "Active";
    public int CurrentRound { get; set; }
    public int CurrentTurnIndex { get; set; }
    public List<CombatParticipantResponse> Participants { get; set; } = new();
    public List<CombatLogEventResponse> Events { get; set; } = new();
}

public class CombatParticipantResponse
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string ParticipantType { get; set; } = string.Empty;
    public int CurrentHP { get; set; }
    public int MaxHP { get; set; }
    public int AC { get; set; }
    public int Initiative { get; set; }
    public List<ConditionEntryResponse> Conditions { get; set; } = new();
    public bool IsCurrentTurn { get; set; }
    public bool IsDead { get; set; }
    // Action economy
    public int ActionsRemaining { get; set; } = 1;
    public int BonusActionsRemaining { get; set; } = 0;
    public int ReactionsRemaining { get; set; } = 1;
    public int MovementsRemaining { get; set; } = 1;
    // Death saves
    public int DeathSaveSuccesses { get; set; }
    public int DeathSaveFailures { get; set; }
}

public class ConditionEntryResponse
{
    public string Name { get; set; } = string.Empty;
    public int Duration { get; set; }
    public string? Description { get; set; }
}

public class CombatLogEventResponse
{
    public Guid Id { get; set; }
    public int Round { get; set; }
    public int TurnIndex { get; set; }
    public string Type { get; set; } = string.Empty;
    public string ActorName { get; set; } = string.Empty;
    public string TargetName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class CombatAttackResponse
{
    public string Attacker { get; set; } = string.Empty;
    public string Weapon { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public bool Hit { get; set; }
    public bool IsCritical { get; set; }
    public bool IsFumble { get; set; }
    public int AttackRoll { get; set; }
    public int AttackDice { get; set; }
    public int AC { get; set; }
    public string DamageDice { get; set; } = string.Empty;
    public int DamageTotal { get; set; }
    public string DamageInfo { get; set; } = string.Empty;
    public int TargetHP { get; set; }
    public int TargetMaxHP { get; set; }
}

public class CombatSaveThrowResponse
{
    public string Participant { get; set; } = string.Empty;
    public string SaveType { get; set; } = string.Empty;
    public int DiceRoll { get; set; }
    public int DC { get; set; }
    public bool Success { get; set; }
}

public class CombatDeathSaveResponse
{
    public string Participant { get; set; } = string.Empty;
    public bool Success { get; set; }
    public int Successes { get; set; }
    public int Failures { get; set; }
    public bool IsStabilized { get; set; }
    public bool IsDead { get; set; }
}

public class CombatSummary
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public string Status { get; set; } = "Active";
    public int CurrentRound { get; set; }
    public int ParticipantCount { get; set; }
    public DateTime StartedAt { get; set; }
}

// ============= Spell Response DTOs =============

public class CombatSpellCastResponse
{
    public string Caster { get; set; } = string.Empty;
    public string SpellName { get; set; } = string.Empty;
    public string SpellLevel { get; set; } = "0";
    public string Target { get; set; } = string.Empty;
    public string SaveType { get; set; } = string.Empty;
    public int SaveDC { get; set; }
    public bool SaveSuccess { get; set; }
    public bool IsCritical { get; set; }
    public string DamageType { get; set; } = string.Empty;
    public int DamageTotal { get; set; }
    public string DamageInfo { get; set; } = string.Empty;
    public string Effect { get; set; } = string.Empty;
    public int? TargetHP { get; set; }
    public int? TargetMaxHP { get; set; }
}

// ============= Progression Response DTOs =============

public class CombatLevelUpResponse
{
    public Guid ParticipantId { get; set; }
    public int NewLevel { get; set; }
    public string SystemId { get; set; } = string.Empty;
}

// ============= Rest Response DTOs =============

public class CombatRestStatusResponse
{
    public string RestType { get; set; } = string.Empty;
    public bool IsInProgress { get; set; }
    public int RoundsRemaining { get; set; }
    public int HPRecovered { get; set; }
    public List<string> Effects { get; set; } = new();
}

// ============= Grid Response DTOs =============

public class CombatGridPositionResponse
{
    public Guid ParticipantId { get; set; }
    public int GridX { get; set; }
    public int GridY { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public int MoveSpeed { get; set; }
}

// ============= AI Combat Response DTOs =============

public class CombatAISuggestionsResponse
{
    public Guid CombatId { get; set; }
    public string ThreatLevel { get; set; } = "Low";
    public string RecommendedStrategy { get; set; } = string.Empty;
    public List<Models.AITacticalAction> Suggestions { get; set; } = new();
    public List<Models.AINPCAction> NPCActions { get; set; } = new();
    public List<Models.AICombatWarning> Warnings { get; set; } = new();
}

// ============= SAN Response DTOs =============

public class CombatSANCheckResponse
{
    public string Participant { get; set; } = string.Empty;
    public int CurrentSAN { get; set; }
    public int Roll { get; set; }
    public int DC { get; set; }
    public bool Success { get; set; }
    public bool IsCritical { get; set; }
    public int SANLoss { get; set; }
    public string Effect { get; set; } = string.Empty;
}

public class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message) { }
}

// ============= Character Creation DTOs =============

public class CreateCharacterResponse
{
    public bool Success { get; set; }
    public Guid? CharacterId { get; set; }
    public string? Name { get; set; }
    public string? Class { get; set; }
    public int? Level { get; set; }
    public string? Error { get; set; }
}

public class CharacterCreateInput
{
    public string Name { get; set; } = string.Empty;
    public string Class { get; set; } = string.Empty;
    public int? Level { get; set; }
    public int? CurrentHP { get; set; }
    public int? MaxHP { get; set; }
    public object? Attributes { get; set; }
    public object? Skills { get; set; }
    public object? Inventory { get; set; }
    public string? SystemId { get; set; }
}

// ============= LLM Interaction Log Response DTOs =============

public class LLMInteractionLogResponse
{
    public Guid Id { get; set; }
    public Guid? PresetId { get; set; }
    public string? PresetName { get; set; }
    public string ProviderType { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int? PromptTokens { get; set; }
    public int? CompletionTokens { get; set; }
    public int? TotalTokens { get; set; }
    public int DurationMs { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? SystemPrompt { get; set; }
    public string? UserPrompt { get; set; }
    public string? Response { get; set; }
    public string? RequestJson { get; set; }
    public string? ResponseJson { get; set; }
    public string Origin { get; set; } = string.Empty;
    public Guid? OriginGameId { get; set; }
    public Guid? OriginSessionId { get; set; }
    public string? OriginAgent { get; set; }
    public string? OriginAction { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
}
