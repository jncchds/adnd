namespace Adnd.Server.Services.Combat;

/// <summary>Result of dealing damage to a participant.</summary>
public record DamageResult(Guid ParticipantId, int DamageDealt, int CurrentHP, bool IsDown);

/// <summary>Result of a D&amp;D 5e death saving throw.</summary>
public record DeathSaveResult(
    Guid ParticipantId,
    bool IsStable,
    bool IsDead,
    bool Revived,
    int Successes,
    int Failures);

/// <summary>Result of a Call of Cthulhu 7e sanity check.</summary>
public record SanityCheckResult(Guid ParticipantId, int SanityLost, bool WentInsane, int CurrentSanity);

/// <summary>An AI-generated combat suggestion for a participant.</summary>
public record AICombatSuggestion(string ParticipantName, string Suggestion, string Reasoning);
