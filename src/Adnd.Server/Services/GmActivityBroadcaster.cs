using System.Collections.Concurrent;
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

    /// <summary>The last activity broadcast for a game, or null if it's idle/never started.
    /// A plain SignalR push has no memory — a client that connects (or joins the group)
    /// mid-generation, e.g. a player already sitting in chat when the GM clicks "Start
    /// Game", or navigating there right after, would never receive the broadcasts that
    /// already fired and would show a static "GM Active" for the whole ~30-60s pipeline
    /// even though real work is in progress. This lets a newly (re)joined client catch up.</summary>
    GMActivityDto? GetCurrent(Guid gameId);
}

public class GmActivityBroadcaster(IHubContext<GameHub> hub) : IGmActivityBroadcaster
{
    private static readonly ConcurrentDictionary<Guid, GMActivityDto> _current = new();

    public Task BroadcastAsync(Guid gameId, string step, string? detail = null, CancellationToken ct = default)
    {
        var dto = new GMActivityDto(gameId, step, detail);
        if (step is nameof(SagaStep.Completed) or nameof(SagaStep.Failed))
            _current.TryRemove(gameId, out _);
        else
            _current[gameId] = dto;

        return hub.Clients.Group(gameId.ToString()).SendAsync("GMActivity", dto, ct);
    }

    public Task BroadcastAsync(Guid gameId, SagaStep step, string? detail = null, CancellationToken ct = default) =>
        BroadcastAsync(gameId, step.ToString(), detail, ct);

    public GMActivityDto? GetCurrent(Guid gameId) => _current.GetValueOrDefault(gameId);
}
