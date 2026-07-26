using Adnd.Server.Data;
using Adnd.Server.Events;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Wolverine;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Adnd.Server.Handlers;

public class SagaOrchestratorHandler : IEventHandler<AgentCallQueued>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SagaOrchestratorHandler> _logger;
    private readonly IEventBus _eventBus;

    public SagaOrchestratorHandler(
        IServiceScopeFactory scopeFactory,
        ILogger<SagaOrchestratorHandler> logger,
        IEventBus eventBus)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _eventBus = eventBus;
    }

    public async Task HandleAsync(AgentCallQueued evt, CancellationToken ct)
    {
        _logger.LogInformation("[SAGA] Orchestrating | SagaId={SagaId}", evt.SagaId);

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var call = await context.AgentCalls
            .FirstOrDefaultAsync(c => c.Id == evt.SagaId && c.GameId == evt.GameId, ct);

        if (call == null)
        {
            _logger.LogWarning("[SAGA] CallNotFound | SagaId={SagaId}", evt.SagaId);
            return;
        }

        call.CurrentStep = SagaStep.Init;
        await context.SaveChangesAsync(ct);

        // Extract system/user prompts from call.Input if it's a GMDispatchOptions JSON
        string? systemPrompt = null;
        string? userPrompt = null;
        if (!string.IsNullOrWhiteSpace(call.Input))
        {
            try
            {
                var options = JsonSerializer.Deserialize<GMDispatchOptions>(call.Input);
                systemPrompt = options?.SystemPrompt;
                userPrompt = options?.UserPrompt;
            }
            catch
            {
                // Input is not GMDispatchOptions — ignore
            }
        }

        // Build party context for subsequent narrations (not for OpenNarrative — that fires before characters exist)
        string? characterContext = null;
        if (call.Action is AgentAction.Narrate or AgentAction.Generate)
        {
            var characters = await context.Characters
                .Include(c => c.Player)
                .Where(c => c.Player!.GameId == call.GameId && !c.IsDeleted)
                .ToListAsync(ct);
            if (characters.Count > 0)
            {
                var lines = characters.Select(c =>
                    $"- {c.Name} ({c.Class}, Level {c.Level}{(c.Background != null ? $", {c.Background}" : "")})");
                characterContext = "\n\nCurrent party members:\n" + string.Join("\n", lines);
            }
        }

        IGameEvent? nextEvent = call.Action switch
        {
            AgentAction.OpenNarrative
                => new LLMDispatchRequested(evt.SagaId, evt.GameId,
                    systemPrompt ?? "You are the Game Master for a TTRPG session.",
                    userPrompt ?? "Generate the opening narrative for this game session.", null),
            AgentAction.Narrate or AgentAction.Generate
                => new LLMDispatchRequested(evt.SagaId, evt.GameId,
                    "You are the Game Master for a TTRPG session." + (characterContext ?? ""),
                    call.Input ?? "Continue the narrative.", null),
            AgentAction.GenerateInitialThreads
                => new LLMDispatchRequested(evt.SagaId, evt.GameId,
                    systemPrompt ?? "You are the Game Master for a TTRPG session. Generate initial plot threads.",
                    userPrompt ?? "Generate initial plot threads.", null),
            AgentAction.Nudge => new LLMDispatchRequested(evt.SagaId, evt.GameId, "You are the Game Master for a TTRPG session. The creator has sent a narrative nudge.", call.Input ?? "Incorporate the direction.", null),
            AgentAction.Query => new LLMDispatchRequested(evt.SagaId, evt.GameId, "You are a helpful TTRPG assistant.", call.Input ?? "Answer the question.", null),
            AgentAction.Suggest => new LLMDispatchRequested(evt.SagaId, evt.GameId, "You are a creative TTRPG Game Master assistant.", call.Input ?? "Suggest plot continuations.", null),
            AgentAction.ManageState => null,
            _ => new AgentCallFailed(evt.SagaId, evt.GameId, $"Unknown action: {call.Action}")
        };

        if (nextEvent != null)
        {
            await _eventBus.PublishAsync(nextEvent, ct);
        }

        call.CurrentStep = nextEvent == null ? SagaStep.Completed : SagaStep.LLMDispatchRequested;
        await context.SaveChangesAsync(ct);

        _logger.LogInformation("[SAGA] NextStep | SagaId={SagaId} | Step={Step}", evt.SagaId, call.CurrentStep);
    }
}
