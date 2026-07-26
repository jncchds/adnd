namespace Adnd.Server.Services.Combat;

public interface ICombatSpellService
{
    Task CastSpellAsync(Guid participantId, string spellName, int spellLevel, CancellationToken ct = default);
    Task ConsumeSpellSlotAsync(Guid participantId, int level, CancellationToken ct = default);
}
