namespace Adnd.Server.Hubs;

public record MessageDto(Guid Id, Guid SessionId, Guid? PlayerId, string Content, string Type, bool IsOOC, DateTimeOffset CreatedAt, object? Metadata);
public record GameStatusDto(Guid GameId, string Status, string GMStatus);
public record GMStatusDto(Guid GameId, string Status, string? LastAction);

// Per-turn progress, distinct from GMStatusDto's persistent Running/Paused toggle — this is
// "what is the current agent call doing right now" so the chat UI can show the player
// something more useful than a static "GM Active" badge while a turn is in flight.
public record GMActivityDto(Guid GameId, string Step, string? Detail);
public record PlayerDto(Guid Id, Guid GameId, Guid UserId, string CharacterName, string Role, bool IsConnected);
// Participants arrive in turn order; CurrentTurnIndex indexes into that list.
public record CombatDto(Guid Id, Guid GameId, Guid SessionId, string Name, string Status, int CurrentRound, int CurrentTurnIndex, List<CombatZoneDto> Zones, List<ParticipantDto> Participants);
public record CombatZoneDto(string Id, string Name, IReadOnlyList<string> Tags);

// Carries the per-turn resources and conditions the combat panel renders. These were
// absent, so the UI displayed "undefined action / undefined bonus / undefinedft".
public record ParticipantDto(
    Guid Id,
    string DisplayName,
    float Initiative,
    int HP,
    int MaxHP,
    int AC,
    string ParticipantType,
    IReadOnlyList<string> Conditions,
    int ActionsRemaining,
    int BonusActionsRemaining,
    int ReactionsRemaining,
    int MovementsRemaining,
    DeathSaveStateDto? DeathSaveState,
    string? ZoneId,
    Guid? CharacterId,
    Guid? PlayerId);

public record DeathSaveStateDto(int Successes, int Failures, bool IsDead, bool IsStable);
public record RollRequestDto(Guid AgentCallId, Guid GameId, Guid? TargetPlayerId, string Formula, string Reason);
public record DiceResultDto(string Formula, int Total, string Breakdown);
