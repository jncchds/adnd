using System.Text.Json;
using Adnd.Server.Data;
using Adnd.Server.Events;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Adnd.Server.Handlers;

public class ToolExecutionHandler(
    AppDbContext db,
    IGMToolRegistry toolRegistry,
    IPlayerRollService playerRolls,
    IRollPromptService prompts,
    IEventBus eventBus,
    ISessionManagementService sessions,
    IGmActivityBroadcaster activity,
    ILogger<ToolExecutionHandler> logger)
{
    public async Task HandleAsync(ToolCallRequested msg, CancellationToken ct)
    {
        var call = await db.AgentCalls.FindAsync([msg.AgentCallId], ct);
        if (call == null) return;

        call.AdvanceStep(SagaStep.ToolExecution);
        await db.SaveChangesAsync(ct);
        await activity.BroadcastAsync(msg.GameId, SagaStep.ToolExecution, msg.ToolName, ct);

        var arguments = JsonSerializer.Deserialize<JsonElement>(msg.ArgumentsJson);

        if (!toolRegistry.IsPlayerRoll(msg.ToolName))
        {
            await ExecuteAndPublishAsync(msg.AgentCallId, msg.GameId, msg.ToolName, msg.ArgumentsJson, msg.ToolIndex, ct);
            return;
        }

        var pendingSession = await sessions.GetOrCreateCurrentSessionAsync(msg.GameId);
        var plan = await toolRegistry.PlanPlayerRollAsync(msg.ToolName, arguments, msg.GameId, pendingSession.Id, ct);
        if (plan.Request is not { } request)
        {
            // Nobody to roll for — the model named someone who isn't there. Told so, it can
            // correct the call on its follow-up instead of the turn stalling on an ask no
            // player will ever see.
            await PublishToolCompletedAsync(msg.AgentCallId, msg.GameId, msg.ToolName, plan.Error ?? "The roll could not be made.", msg.ToolIndex, ct);
            return;
        }

        // Mandatory rolls fall straight through this: the player is not asked whether the
        // rules may apply to them, only whether they want to spend an ability afterwards.
        if (toolRegistry.RequiresConfirmation(msg.ToolName, arguments))
        {
            var targetPlayerId = request.PlayerId;

            // Record the pending tool call so it can be surfaced via /api/gmtools/pending
            // and resolved later. Without this the saga simply stalled until the 5-minute timeout.
            var toolCall = new GMToolCall
            {
                AgentCallId = msg.AgentCallId,
                GameId = msg.GameId,
                SessionId = pendingSession.Id,
                ToolName = msg.ToolName,
                Arguments = arguments,
                ToolIndex = msg.ToolIndex,
                RequiresConfirmation = true,
                Status = GMToolCallStatus.AwaitingConfirmation,
                TargetPlayerId = targetPlayerId
            };
            db.GMToolCalls.Add(toolCall);
            await db.SaveChangesAsync(ct);

            // The ask goes into the chat log addressed to that player, and the roll will
            // replace it there.
            if (targetPlayerId is { } askedPlayer)
            {
                var promptId = await prompts.AskAsync(msg.GameId, pendingSession.Id, askedPlayer, "RollRequest",
                    DescribeRequest(request),
                    new
                    {
                        kind = "rollRequest",
                        toolCallId = toolCall.Id,
                        formula = request.Formula,
                        dc = request.Dc,
                        reason = request.Reason
                    }, ct);

                toolCall.PromptMessageId = promptId;
                await db.SaveChangesAsync(ct);
            }

            await eventBus.PublishAsync(new ToolCallWaitingConfirmation(msg.AgentCallId, msg.GameId, msg.ToolName, msg.ArgumentsJson, targetPlayerId), ct);
            return;
        }

        // Mandatory: no prompt was ever posted, so the roll posts its own message.
        await RollForPlayerAsync(msg.AgentCallId, msg.GameId, msg.ToolName, arguments, msg.ToolIndex, null, ct);
    }

    /// <summary>Resumes the chain once a player confirms or declines a gated tool call.</summary>
    public async Task HandleAsync(ToolCallConfirmationResolved msg, CancellationToken ct)
    {
        var promptMessageId = await db.GMToolCalls
            .Where(t => t.AgentCallId == msg.AgentCallId && t.ToolIndex == msg.ToolIndex)
            .Select(t => t.PromptMessageId)
            .FirstOrDefaultAsync(ct);

        if (!msg.Approved)
        {
            var reason = string.IsNullOrWhiteSpace(msg.DeclineReason) ? "Player declined." : msg.DeclineReason;

            // Resolved privately rather than withdrawn: the player keeps a record of the ask
            // and their answer, and the table is not told about a roll that never happened.
            if (promptMessageId is { } declinedPrompt)
                await prompts.ResolveAsync(msg.GameId, declinedPrompt, "RollDecline",
                    $"You declined this roll. ({reason})", null, makePublic: false, ct);

            await eventBus.PublishAsync(new ToolCallCompleted(msg.AgentCallId, msg.GameId, msg.ToolName, $"Declined: {reason}", msg.ToolIndex, 0), ct);
            return;
        }

        if (toolRegistry.IsPlayerRoll(msg.ToolName))
        {
            var arguments = JsonSerializer.Deserialize<JsonElement>(msg.ArgumentsJson);
            await RollForPlayerAsync(msg.AgentCallId, msg.GameId, msg.ToolName, arguments, msg.ToolIndex, promptMessageId, ct);
            return;
        }

        await ExecuteAndPublishAsync(msg.AgentCallId, msg.GameId, msg.ToolName, msg.ArgumentsJson, msg.ToolIndex, ct);
    }

    /// <summary>The wording of the ask itself, which the player sees in their log until they
    /// answer it.</summary>
    private static string DescribeRequest(PlayerRollRequest request)
    {
        var target = request.Dc is { } dc ? $" vs DC {dc}" : "";
        var why = string.IsNullOrWhiteSpace(request.Reason) ? "" : $" — {request.Reason}";
        return $"The GM asks you to roll {request.Formula}{target}{why}";
    }

    /// <summary>Resumes the chain once the player has taken or waived their reroll.</summary>
    public async Task HandleAsync(RerollResolved msg, CancellationToken ct)
    {
        var content = msg.InterimContent;
        var total = msg.InterimTotal;
        var arguments = JsonSerializer.Deserialize<JsonElement>(msg.ArgumentsJson);

        if (msg.FeatureId is null)
        {
            // Kept the roll. The offer resolved into nothing the table needs — the roll it
            // referred to is already in the log — so it is simply withdrawn.
            if (msg.PromptMessageId is { } keptPrompt)
                await prompts.WithdrawAsync(msg.GameId, keptPrompt, ct);
        }
        else
        {
            var session = await sessions.GetOrCreateCurrentSessionAsync(msg.GameId);
            var plan = await toolRegistry.PlanPlayerRollAsync(msg.ToolName, arguments, msg.GameId, session.Id, ct);
            var rerolled = plan.Request is { } request
                ? await playerRolls.RerollAsync(request with { PostMessage = msg.PromptMessageId is null }, msg.CharacterId, msg.FeatureId, ct)
                : null;

            if (rerolled is not null)
            {
                content = rerolled.Content;
                total = rerolled.Total;
                if (msg.PromptMessageId is { } promptId)
                    await prompts.ResolveAsync(msg.GameId, promptId, "DiceRoll", rerolled.Content,
                        new { total = rerolled.Total, dc = rerolled.Dc, success = rerolled.Success },
                        makePublic: true, ct);
            }
            else
            {
                // The ability was gone by the time this arrived. The original roll stands;
                // saying so beats silently pretending the reroll was never requested.
                logger.LogWarning("Reroll '{FeatureId}' unavailable for character {CharacterId}; keeping the original roll",
                    msg.FeatureId, msg.CharacterId);

                if (msg.PromptMessageId is { } failedPrompt)
                    await prompts.ResolveAsync(msg.GameId, failedPrompt, "RollDecline",
                        "That ability was no longer available — the original roll stands.",
                        null, makePublic: false, ct);
            }
        }

        content = await FinishAsync(msg.ToolName, arguments, msg.GameId, total, content, ct);
        await PublishToolCompletedAsync(msg.AgentCallId, msg.GameId, msg.ToolName, content, msg.ToolIndex, ct);
    }

    /// <summary>
    /// Rolls on the player's behalf and, if their character can still change the result,
    /// holds the turn open instead of reporting a number that is about to be replaced. The
    /// GM must never narrate a failure the player then rerolls into a success.
    /// </summary>
    private async Task RollForPlayerAsync(
        Guid agentCallId, Guid gameId, string toolName, JsonElement arguments, int toolIndex, Guid? promptMessageId, CancellationToken ct)
    {
        try
        {
            var session = await sessions.GetOrCreateCurrentSessionAsync(gameId);
            var plan = await toolRegistry.PlanPlayerRollAsync(toolName, arguments, gameId, session.Id, ct);
            if (plan.Request is null)
            {
                // Planned fine when asked; the situation changed while the player decided.
                await PublishToolCompletedAsync(agentCallId, gameId, toolName, plan.Error ?? "The roll could not be made.", toolIndex, ct);
                return;
            }
            var request = plan.Request with { PostMessage = promptMessageId is null };

            var result = await playerRolls.RollAsync(request, ct);

            // The ask becomes the answer, in place and now visible to the table.
            if (promptMessageId is { } promptId)
                await prompts.ResolveAsync(gameId, promptId, "DiceRoll", result.Content,
                    new { total = result.Total, dc = result.Dc, success = result.Success },
                    makePublic: true, ct);

            if (result.RerollOptions.Count == 0 || result.CharacterId is not { } characterId)
            {
                var final = await FinishAsync(toolName, arguments, gameId, result.Total, result.Content, ct);
                await PublishToolCompletedAsync(agentCallId, gameId, toolName, final, toolIndex, ct);
                return;
            }

            var toolCall = new GMToolCall
            {
                AgentCallId = agentCallId,
                GameId = gameId,
                SessionId = session.Id,
                ToolName = toolName,
                Arguments = arguments,
                ToolIndex = toolIndex,
                Status = GMToolCallStatus.AwaitingReroll,
                TargetPlayerId = request.PlayerId,
                RerollCharacterId = characterId,
                Result = JsonSerializer.SerializeToElement(new
                {
                    content = result.Content,
                    total = result.Total,
                    success = result.Success,
                    options = result.RerollOptions
                })
            };
            db.GMToolCalls.Add(toolCall);
            await db.SaveChangesAsync(ct);

            // A second, separate message: the roll already stands in the log, and this asks
            // whether to amend it. Addressed to the roller alone — a reroll is their resource,
            // and putting it in front of the table would be an invitation to vote on it.
            if (request.PlayerId is { } playerId)
            {
                toolCall.PromptMessageId = await prompts.AskAsync(gameId, session.Id, playerId, "RerollOffer",
                    $"{result.Content}\n\nYou can reroll this.",
                    new { kind = "rerollOffer", toolCallId = toolCall.Id, options = result.RerollOptions }, ct);
                await db.SaveChangesAsync(ct);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Player roll failed for agent call {AgentCallId}", agentCallId);
            await PublishToolCompletedAsync(agentCallId, gameId, toolName, $"Error: {ex.Message}", toolIndex, ct);
        }
    }

    /// <summary>Applies the settled number — a death save is recorded only once any reroll is
    /// done — and never lets a failure there lose the roll the player already saw.</summary>
    private async Task<string> FinishAsync(string toolName, JsonElement arguments, Guid gameId, int total, string content, CancellationToken ct)
    {
        try
        {
            return await toolRegistry.FinishPlayerRollAsync(toolName, arguments, gameId, total, content, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Applying {ToolName} result failed for game {GameId}", toolName, gameId);
            return $"{content}\nError applying the result: {ex.Message}";
        }
    }

    private async Task PublishToolCompletedAsync(Guid agentCallId, Guid gameId, string toolName, string content, int toolIndex, CancellationToken ct)
    {
        var totalTools = await db.ToolCallCoordinators
            .Where(c => c.AgentCallId == agentCallId)
            .Select(c => c.TotalTools)
            .FirstOrDefaultAsync(ct);

        await eventBus.PublishAsync(new ToolCallCompleted(agentCallId, gameId, toolName, content, toolIndex, totalTools), ct);
    }

    private async Task ExecuteAndPublishAsync(Guid agentCallId, Guid gameId, string toolName, string argumentsJson, int toolIndex, CancellationToken ct)
    {
        try
        {
            var session = await sessions.GetOrCreateCurrentSessionAsync(gameId);
            var args = JsonSerializer.Deserialize<JsonElement>(argumentsJson);
            var result = await toolRegistry.ExecuteToolAsync(toolName, args, gameId, session.Id, ct);
            await PublishToolCompletedAsync(agentCallId, gameId, toolName, result, toolIndex, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Tool '{ToolName}' execution failed for agent call {AgentCallId}", toolName, agentCallId);
            await PublishToolCompletedAsync(agentCallId, gameId, toolName, $"Error: {ex.Message}", toolIndex, ct);
        }
    }
}
