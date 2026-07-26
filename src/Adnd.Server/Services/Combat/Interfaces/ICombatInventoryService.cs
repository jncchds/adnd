namespace Adnd.Server.Services.Combat;

public interface ICombatInventoryService
{
    Task UseItemAsync(Guid participantId, string itemName, CancellationToken ct = default);
}
