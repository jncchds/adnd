namespace Adnd.Server.Services.Combat;

public interface ISANService
{
    Task<SanityCheckResult> PerformSanityCheckAsync(
        Guid participantId,
        int sanityLoss,
        string trigger,
        CancellationToken ct = default);
}
