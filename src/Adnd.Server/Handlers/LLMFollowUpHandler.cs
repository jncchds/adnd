using Adnd.Server.Data;
using Adnd.Server.Events;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Wolverine;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Adnd.Server.Handlers;

public class LLMFollowUpHandler : IEventHandler<LLMFollowUpRequested>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILLMProviderFactory _providerFactory;
    private readonly ILogger<LLMFollowUpHandler> _logger;
    private readonly IEventBus _eventBus;

    public LLMFollowUpHandler(IServiceScopeFactory scopeFactory, ILLMProviderFactory providerFactory, ILogger<LLMFollowUpHandler> logger, IEventBus eventBus)
    {
        _scopeFactory = scopeFactory;
        _providerFactory = providerFactory;
        _logger = logger;
        _eventBus = eventBus;
    }

    public async Task HandleAsync(LLMFollowUpRequested evt, CancellationToken ct)
    {
        _logger.LogInformation("[LLM] FollowUp | SagaId={SagaId} | ToolResults={Count}", evt.SagaId, evt.ToolResults.Count);

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var call = await context.AgentCalls
            .Include(c => c.Game)
            .ThenInclude(g => g!.LLMPreset)
            .FirstOrDefaultAsync(c => c.Id == evt.SagaId, ct);

        if (call?.Game == null || call.Game.LLMPreset == null)
        {
            await PublishFailure(scope, evt.SagaId, evt.GameId, "LLM preset not configured", context, ct);
            return;
        }

        var provider = GetProvider(call.Game.LLMPreset);
        if (provider == null)
        {
            await PublishFailure(scope, evt.SagaId, evt.GameId, $"LLM provider '{call.Game.LLMPreset.ProviderType}' not available", context, ct);
            return;
        }

        var toolResultsText = string.Join("\n", evt.ToolResults.Select(tr =>
            $"Tool '{tr.ToolName}': {tr.Result}"));
        var userPrompt = $"Tool results:\n{toolResultsText}\n\nNow continue the narrative based on these results.";

        string result;
        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            result = await provider.CompleteAsync(
                "You are the Game Master for a TTRPG session.",
                userPrompt,
                null);
            sw.Stop();

            var interactionLogger = scope.ServiceProvider.GetRequiredService<ILLMInteractionLogger>();
            var tokenUsage = provider.GetTokenUsage(result);
            await interactionLogger.LogInteractionAsync(
                call.Game.CreatorId, call.Game.LLMPresetId, provider.ProviderId,
                call.Game.LLMPreset.BaseModel,
                tokenUsage?.promptTokens, tokenUsage?.completionTokens, tokenUsage?.totalTokens,
                (int)sw.ElapsedMilliseconds,
                "You are the Game Master for a TTRPG session.",
                userPrompt, result,
                null, provider.EndpointUrl, "saga", call.GameId, call.SessionId,
                "LLMFollowUpHandler", "follow_up",
                call.Game.LLMPreset.Name, call.Game.LLMPreset.EndpointUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[LLM] FollowUpFailed | SagaId={SagaId}", evt.SagaId);
            await PublishFailure(scope, evt.SagaId, evt.GameId, ex.Message, context, ct);
            return;
        }

        var nextEvent = new NarrativeReady(evt.SagaId, evt.GameId, result);
        await _eventBus.PublishAsync(nextEvent, ct);

        call.CurrentStep = SagaStep.NarrativeReady;
        await context.SaveChangesAsync(ct);

        _logger.LogInformation("[LLM] FollowUpDone | SagaId={SagaId}", evt.SagaId);
    }

    private ILLMProvider? GetProvider(LLMPreset preset) => _providerFactory.CreateFromPreset(preset);

    private async Task PublishFailure(IServiceScope scope, Guid sagaId, Guid gameId, string error, AppDbContext context, CancellationToken ct)
    {
        var call = await context.AgentCalls.FirstOrDefaultAsync(c => c.Id == sagaId, ct);
        if (call != null)
        {
            call.Status = AgentCallStatus.Failed;
            call.Error = error;
            call.CompletedAt = DateTime.UtcNow;
            call.CurrentStep = SagaStep.Failed;
            await context.SaveChangesAsync(ct);
        }

        var nextEvent = new AgentCallFailed(sagaId, gameId, error);
        await _eventBus.PublishAsync(nextEvent, ct);
    }
}
