using Adnd.Server.Data;
using Adnd.Server.Events;
using Adnd.Server.Hubs;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Adnd.Server.Handlers;

public class NarrativeHandler : IEventHandler<NarrativeReady>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<GameHub> _hubContext;
    private readonly IEventBus _eventBus;
    private readonly ILogger<NarrativeHandler> _logger;

    public NarrativeHandler(IServiceScopeFactory scopeFactory, IHubContext<GameHub> hubContext, IEventBus eventBus, ILogger<NarrativeHandler> logger)
    {
        _scopeFactory = scopeFactory;
        _hubContext = hubContext;
        _eventBus = eventBus;
        _logger = logger;
    }

    public async Task HandleAsync(NarrativeReady evt, CancellationToken ct)
    {
        _logger.LogInformation("[NARRATIVE] Ready | SagaId={SagaId}", evt.SagaId);

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var call = await context.AgentCalls.FirstOrDefaultAsync(c => c.Id == evt.SagaId, ct);
        if (call == null) return;

        call.CurrentStep = SagaStep.Completed;
        call.Output = evt.Narrative;
        call.OutputMessage = evt.Narrative;
        call.CompletedAt = DateTime.UtcNow;
        var startedAt = call.StartedAt ?? call.CreatedAt;
        call.DurationMs = (int)(call.CompletedAt.Value - startedAt).TotalMilliseconds;

        await context.SaveChangesAsync(ct);

        // Trigger Starting → Active game status transition
        await _eventBus.PublishAsync(new GameNarrationStarted(call.GameId, call.Id), ct);

        // Broadcast to game
        await _hubContext.Clients.Group(call.GameId.ToString()).SendAsync("GameNarration", new
        {
            Id = call.Id,
            call.GameId,
            call.SessionId,
            Content = evt.Narrative,
            MessageType = (int)Adnd.Server.Events.MessageType.AgentResponse,
            Timestamp = DateTime.UtcNow
        });

        _logger.LogInformation("[NARRATIVE] Broadcast | SagaId={SagaId} | Len={Len}", evt.SagaId, evt.Narrative.Length);
    }
}
