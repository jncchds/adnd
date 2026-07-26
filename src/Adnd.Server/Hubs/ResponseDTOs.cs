namespace Adnd.Server.Hubs;

public record MessageDto(Guid Id, Guid SessionId, Guid? PlayerId, string Content, string Type, bool IsOOC, DateTimeOffset CreatedAt, object? Metadata);
public record GameStatusDto(Guid GameId, string Status, string GMStatus);
public record GMStatusDto(Guid GameId, string Status, string? LastAction);
public record PlayerDto(Guid Id, Guid GameId, Guid UserId, string CharacterName, string Role, bool IsConnected);
public record CombatDto(Guid Id, Guid GameId, string Name, string Status, int CurrentRound, int CurrentTurnIndex, List<ParticipantDto> Participants);
public record ParticipantDto(Guid Id, string DisplayName, float Initiative, int HP, int MaxHP, int AC, string ParticipantType);
public record DamageDto(Guid CombatId, Guid TargetId, int Amount, string DamageType, int NewHP);
public record ConditionDto(Guid CombatId, Guid ParticipantId, string Condition, bool Applied);
public record RollRequestDto(Guid AgentCallId, Guid GameId, Guid? TargetPlayerId, string Formula, string Reason);
public record DiceResultDto(string Formula, int Total, string Breakdown);
