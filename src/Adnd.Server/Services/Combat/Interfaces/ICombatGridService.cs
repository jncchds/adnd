namespace Adnd.Server.Services.Combat;

public interface ICombatGridService
{
    Task SetGridAsync(Guid combatId, int width, int height, CancellationToken ct = default);
    Task SetPositionAsync(Guid participantId, int x, int y, CancellationToken ct = default);
    Task MoveAsync(Guid participantId, int dx, int dy, CancellationToken ct = default);
}
