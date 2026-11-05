using System.Text.Json;
using Adnd.Server.Data;
using Adnd.Server.Events;
using Adnd.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Services;

/// <summary>How an answer to a roll prompt went: <see cref="Ok"/>, or why not (and whether the caller may even see it).</summary>
public sealed record RollAnswer(bool Ok, string? Error = null, bool NotFound = false, bool Forbidden = false)
{
    public static readonly RollAnswer Done = new(true);
}

/// <summary>
/// A player answering the GM's roll prompt — roll or decline a requested roll, take or waive a reroll. The web
/// chat (<c>GMToolController</c>) and the Telegram bot's buttons both come through here: one set of checks that the
/// prompt is still open and that it was asked of the caller.
/// </summary>
public interface IRollAnswerService
{
    Task<RollAnswer> ConfirmAsync(Guid toolCallId, Guid userId, bool approved, string? reason, CancellationToken ct = default);

    /// <param name="featureId">The ability to spend, or null to keep the original roll.</param>
    Task<RollAnswer> RerollAsync(Guid toolCallId, Guid userId, string? featureId, CancellationToken ct = default);
}

public class RollAnswerService(AppDbContext db, IGameAuthorizationService auth, IEventBus eventBus) : IRollAnswerService
{
    public async Task<RollAnswer> ConfirmAsync(Guid toolCallId, Guid userId, bool approved, string? reason, CancellationToken ct = default)
    {
        var toolCall = await db.GMToolCalls.FirstOrDefaultAsync(t => t.Id == toolCallId, ct);
        if (toolCall is null)
            return new RollAnswer(false, "Tool call not found.", NotFound: true);

        // The roll was asked of one player. Membership alone let anyone at the table roll —
        // or decline — on someone else's behalf.
        Player caller;
        try
        {
            caller = await auth.RequirePlayerAsync(toolCall.GameId, userId);
        }
        catch (UnauthorizedAccessException ex)
        {
            return new RollAnswer(false, ex.Message, Forbidden: true);
        }

        if (toolCall.TargetPlayerId is { } target && target != caller.Id)
            return new RollAnswer(false, "This roll was asked of another player.", Forbidden: true);

        if (toolCall.Status != GMToolCallStatus.AwaitingConfirmation)
            return new RollAnswer(false, "This tool call has already been resolved.");

        toolCall.Status = approved ? GMToolCallStatus.Completed : GMToolCallStatus.Declined;
        toolCall.CompletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        // Hands control back to ToolExecutionHandler so the saga can continue.
        if (toolCall.AgentCallId is { } agentCallId)
        {
            await eventBus.PublishAsync(new ToolCallConfirmationResolved(
                agentCallId, toolCall.GameId, toolCall.ToolName,
                toolCall.Arguments.GetRawText(), toolCall.ToolIndex, approved, reason), ct);
        }

        return RollAnswer.Done;
    }

    /// <summary>
    /// Take, or waive, the reroll offered after a player roll. Either way the GM's turn — held open precisely so it
    /// never narrates a number the player is about to replace — resumes here.
    /// </summary>
    public async Task<RollAnswer> RerollAsync(Guid toolCallId, Guid userId, string? featureId, CancellationToken ct = default)
    {
        var toolCall = await db.GMToolCalls.FirstOrDefaultAsync(t => t.Id == toolCallId, ct);
        if (toolCall is null)
            return new RollAnswer(false, "Tool call not found.", NotFound: true);

        if (toolCall.Status != GMToolCallStatus.AwaitingReroll)
            return new RollAnswer(false, "This roll is not awaiting a reroll.");

        // The offer belongs to one player. Authorizing only on game membership would let any
        // player at the table spend someone else's Lucky points.
        Player caller;
        try
        {
            caller = await auth.RequirePlayerAsync(toolCall.GameId, userId);
        }
        catch (UnauthorizedAccessException ex)
        {
            return new RollAnswer(false, ex.Message, Forbidden: true);
        }

        if (toolCall.TargetPlayerId is { } target && target != caller.Id)
            return new RollAnswer(false, "This reroll was offered to another player.", Forbidden: true);

        featureId = string.IsNullOrWhiteSpace(featureId) ? null : featureId;

        // Only an ability that was actually offered may be taken — otherwise the client could
        // name any id and the roll would be reattempted under an ability the trigger never fired for.
        if (featureId is not null && !WasOffered(toolCall.Result, featureId))
            return new RollAnswer(false, "That ability was not offered for this roll.");

        toolCall.Status = GMToolCallStatus.Completed;
        toolCall.CompletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        if (toolCall.AgentCallId is { } agentCallId && toolCall.RerollCharacterId is { } characterId)
        {
            await eventBus.PublishAsync(new RerollResolved(
                agentCallId, toolCall.GameId, toolCall.Arguments.GetRawText(), toolCall.ToolIndex,
                characterId, featureId, ReadInterimContent(toolCall.Result), toolCall.PromptMessageId,
                toolCall.ToolName, ReadInterimTotal(toolCall.Result), ReadInterimNatural(toolCall.Result)), ct);
        }

        return RollAnswer.Done;
    }

    private static bool WasOffered(JsonElement result, string featureId)
        => result.ValueKind == JsonValueKind.Object
           && result.TryGetProperty("options", out var options)
           && options.ValueKind == JsonValueKind.Array
           && options.EnumerateArray().Any(o =>
               (o.TryGetProperty("featureId", out var id) || o.TryGetProperty("FeatureId", out id))
               && string.Equals(id.GetString(), featureId, StringComparison.OrdinalIgnoreCase));

    /// <summary>The id of the <paramref name="index"/>th ability offered (the Telegram buttons carry the index).</summary>
    public static string? OfferedFeatureId(JsonElement result, int index)
        => result.ValueKind == JsonValueKind.Object
           && result.TryGetProperty("options", out var options)
           && options.ValueKind == JsonValueKind.Array
           && index >= 0 && index < options.GetArrayLength()
           && (options[index].TryGetProperty("featureId", out var id) || options[index].TryGetProperty("FeatureId", out id))
            ? id.GetString()
            : null;

    private static int? ReadInterimNatural(JsonElement result)
        => result.ValueKind == JsonValueKind.Object
           && result.TryGetProperty("natural", out var natural)
           && natural.ValueKind == JsonValueKind.Number && natural.TryGetInt32(out var value) ? value : null;

    private static int ReadInterimTotal(JsonElement result)
        => result.ValueKind == JsonValueKind.Object
           && result.TryGetProperty("total", out var total)
           && total.ValueKind == JsonValueKind.Number
            ? total.GetInt32()
            : 0;

    private static string ReadInterimContent(JsonElement result)
        => result.ValueKind == JsonValueKind.Object
           && result.TryGetProperty("content", out var content)
           && content.ValueKind == JsonValueKind.String
            ? content.GetString() ?? string.Empty
            : string.Empty;
}
