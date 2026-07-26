using Adnd.Server.Models;

namespace Adnd.Server.Services.Combat;

public interface ICombatProgressionService
{
    Task GrantXPAsync(Guid participantId, int amount, CancellationToken ct = default);
    Task<Character?> LevelUpAsync(Guid participantId, CancellationToken ct = default);
}
