using Adnd.Server.Data;
using Adnd.Server.Events;
using Adnd.Server.Models;
using Adnd.Server.Services;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Adnd.Server.Handlers;

public class LLMDispatchHandler : IEventHandler<LLMDispatchRequested>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<LLMDispatchHandler> _logger;
    private readonly IPublishEndpoint _publishEndpoint;

    public LLMDispatchHandler(IServiceProvider serviceProvider, ILogger<LLMDispatchHandler> logger, IPublishEndpoint publishEndpoint)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _publishEndpoint = publishEndpoint;
    }

    public async Task HandleAsync(LLMDispatchRequested evt, CancellationToken ct)
    {
        _logger.LogInformation("[LLM] Dispatching | SagaId={SagaId}", evt.SagaId);

        using var scope = _serviceProvider.CreateScope();
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

        string result;
        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var llmOptions = !string.IsNullOrEmpty(evt.Options)
                ? JsonSerializer.Deserialize<LLMOptions>(evt.Options)
                : null;
            result = await provider.CompleteAsync(evt.SystemPrompt, evt.UserPrompt, llmOptions);
            sw.Stop();

            var interactionLogger = scope.ServiceProvider.GetRequiredService<ILLMInteractionLogger>();
            var tokenUsage = provider.GetTokenUsage(result);
            await interactionLogger.LogInteractionAsync(
                call.Game.CreatorId, call.Game.LLMPresetId, provider.ProviderId,
                llmOptions?.Model ?? call.Game.LLMPreset.BaseModel,
                tokenUsage?.promptTokens, tokenUsage?.completionTokens, tokenUsage?.totalTokens,
                (int)sw.ElapsedMilliseconds, evt.SystemPrompt, evt.UserPrompt, result,
                null, provider.EndpointUrl, "saga", call.GameId, call.SessionId,
                "LLMDispatchHandler", "dispatch",
                call.Game.LLMPreset.Name, call.Game.LLMPreset.EndpointUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[LLM] DispatchFailed | SagaId={SagaId}", evt.SagaId);
            await PublishFailure(scope, evt.SagaId, evt.GameId, ex.Message, context, ct);
            return;
        }

        var hasToolCalls = result.Contains("\"name\":") || result.Contains("tool_calls");
        var toolCallCount = hasToolCalls ? CountToolCalls(result) : 0;

        var nextEvent = new LLMResponseReceived(evt.SagaId, evt.GameId, result, hasToolCalls, toolCallCount);
        await _publishEndpoint.Publish(nextEvent, ct);

        call.CurrentStep = SagaStep.LLMResponseReceived;
        await context.SaveChangesAsync(ct);

        _logger.LogInformation("[LLM] Dispatched | SagaId={SagaId} | HasToolCalls={HasTools} | Count={Count}",
            evt.SagaId, hasToolCalls, toolCallCount);
    }

    private ILLMProvider? GetProvider(LLMPreset preset)
    {
        var factory = _serviceProvider.GetRequiredService<ILLMProviderFactory>();
        return factory.CreateFromPreset(preset);
    }

    private int CountToolCalls(string response)
    {
        var count = 0;
        var pos = 0;
        while ((pos = response.IndexOf("\"name\":", pos, StringComparison.Ordinal)) >= 0)
        {
            count++;
            pos += 7;
        }
        return count;
    }

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
        await _publishEndpoint.Publish(nextEvent, ct);
    }
}
