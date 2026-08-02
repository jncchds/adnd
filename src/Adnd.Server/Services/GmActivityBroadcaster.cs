using Adnd.Server.Hubs;
using Adnd.Server.Models;
using Microsoft.AspNetCore.SignalR;

namespace Adnd.Server.Services;

public interface IGmActivityBroadcaster
{
    /// <summary>Pushes the GM's current activity to the game's chat so players see live
    /// progress instead of a static "GM Active" badge with no indication of what's
    /// happening or whether anything is actually moving. `step` is a free-form label —
    /// either a SagaStep name (see the overload) or a phase name for work that doesn't run
    /// through AgentSaga, like the game-start narration pipeline.</summary>
    Task BroadcastAsync(Guid gameId, string step, string? detail = null, CancellationToken ct = default);

    Task BroadcastAsync(Guid gameId, SagaStep step, string? detail = null, CancellationToken ct = default);
}

public class GmActivityBroadcaster(IHubContext<GameHub> hub) : IGmActivityBroadcaster
{
    public Task BroadcastAsync(Guid gameId, string step, string? detail = null, CancellationToken ct = default) =>
        hub.Clients.Group(gameId.ToString()).SendAsync("GMActivity", new GMActivityDto(gameId, step, detail), ct);

    public Task BroadcastAsync(Guid gameId, SagaStep step, string? detail = null, CancellationToken ct = default) =>
        BroadcastAsync(gameId, step.ToString(), detail, ct);
}
