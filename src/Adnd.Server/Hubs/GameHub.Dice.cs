using System.Collections.Concurrent;
using Adnd.Server.Events;
using Adnd.Server.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Hubs;

public partial class GameHub
{
    /// <summary>
    /// The reroll offered on a roll the player made themselves, keyed game:player. Held in
    /// memory rather than persisted because nothing waits on it — unlike the requestPlayerRoll
    /// offer, which parks a GMToolCall precisely because a saga is holding its turn open. A
    /// restart drops the offer, which reads as the moment having passed.
    /// </summary>
    private static readonly ConcurrentDictionary<string, PendingSelfRoll> PendingSelfRolls = new();

    private sealed record PendingSelfRoll(
        string Formula, int? Dc, string? Reason, bool IsSecret, string MessageType,
        List<string> FeatureIds, Guid PromptMessageId);

    public async Task RollDice(Guid gameId, string formula, bool isSecret = false)
    {
        var player = await RequireMemberAsync(gameId);
        var session = await ResolveGameSessionAsync(gameId);

        var request = new PlayerRollRequest(gameId, session.Id, player.Id, formula, IsSecret: isSecret);
        var result = await playerRolls.RollAsync(request);
        await OfferRerollAsync(request, result);

        await PublishAsync(new DiceRolled(gameId, player.Id, formula, result.Total, result.Content));
    }

    public async Task RollSkillCheck(Guid gameId, string skillId, int dc, bool isSecret = false)
    {
        var player = await RequireMemberAsync(gameId);
        var session = await ResolveGameSessionAsync(gameId);

        // The DC used to be dropped here — this delegated to RollDice with a bare "1d20", so
        // the message never stated what the roll was against and the player had to infer
        // success from the narration.
        var request = new PlayerRollRequest(
            gameId, session.Id, player.Id, "1d20", dc, skillId, isSecret, MessageType: "SkillCheck");
        var result = await playerRolls.RollAsync(request);
        await OfferRerollAsync(request, result);

        await PublishAsync(new SkillCheckRequested(gameId, CurrentUserId, skillId, dc));
    }

    public async Task RollAttack(Guid gameId, int attackBonus, Guid? targetId = null)
    {
        var player = await RequireMemberAsync(gameId);
        var session = await ResolveGameSessionAsync(gameId);

        var request = new PlayerRollRequest(
            gameId, session.Id, player.Id, $"1d20+{attackBonus}", MessageType: "AttackRoll");
        var result = await playerRolls.RollAsync(request);
        await OfferRerollAsync(request, result);

        await PublishAsync(new AttackRequested(gameId, CurrentUserId, targetId, attackBonus));
    }

    /// <summary>
    /// Take a reroll offered on a roll the player made themselves. No agent saga is involved,
    /// so nothing is waiting on the answer — the reroll is simply applied and announced.
    /// </summary>
    public async Task TakeReroll(Guid gameId, string featureId)
    {
        var player = await RequireMemberAsync(gameId);
        var session = await ResolveGameSessionAsync(gameId);

        // Removed rather than read: the offer is single-use, and leaving it in place would let
        // a double-clicked button spend two uses on one roll.
        if (!PendingSelfRolls.TryRemove(SelfRollKey(gameId, player.Id), out var offer))
            throw new HubException("That reroll is no longer available.");

        if (!offer.FeatureIds.Contains(featureId, StringComparer.OrdinalIgnoreCase))
            throw new HubException("That ability was not offered for this roll.");

        var characterId = await db.Characters
            .Where(c => c.PlayerId == player.Id)
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync()
            ?? throw new HubException("You have no character to spend that ability on.");

        // The offer message becomes the rerolled result, in place and now public.
        var result = await playerRolls.RerollAsync(
            new PlayerRollRequest(gameId, session.Id, player.Id, offer.Formula, offer.Dc, offer.Reason,
                offer.IsSecret, offer.MessageType, PostMessage: false),
            characterId, featureId);

        if (result is null)
        {
            await prompts.ResolveAsync(gameId, offer.PromptMessageId, "RollDecline",
                "That ability was no longer available — the original roll stands.",
                null, makePublic: false);
            throw new HubException("You are out of uses of that ability.");
        }

        await prompts.ResolveAsync(gameId, offer.PromptMessageId, offer.MessageType, result.Content,
            new { total = result.Total, dc = result.Dc, success = result.Success, isSecret = offer.IsSecret },
            makePublic: !offer.IsSecret);
    }

    /// <summary>Decline the offer, so a stale one cannot be taken a scene later.</summary>
    public async Task WaiveReroll(Guid gameId)
    {
        var player = await RequireMemberAsync(gameId);
        if (PendingSelfRolls.TryRemove(SelfRollKey(gameId, player.Id), out var offer))
            await prompts.WithdrawAsync(gameId, offer.PromptMessageId);
    }

    private async Task OfferRerollAsync(PlayerRollRequest request, PlayerRollResult result)
    {
        if (result.RerollOptions.Count == 0 || request.PlayerId is not { } playerId) return;

        // A second message rather than an edit of the roll: the roll already stands in the
        // log, and this asks whether to amend it. Addressed to the roller alone — it is their
        // resource, and showing it to the table would be an invitation to vote on it.
        var promptId = await prompts.AskAsync(request.GameId, request.SessionId, playerId, "RerollOffer",
            $"{result.Content}\n\nYou can reroll this.",
            new { kind = "rerollOffer", toolCallId = (Guid?)null, options = result.RerollOptions });

        PendingSelfRolls[SelfRollKey(request.GameId, playerId)] = new PendingSelfRoll(
            request.Formula, request.Dc, request.Reason, request.IsSecret, request.MessageType,
            result.RerollOptions.Select(o => o.FeatureId).ToList(), promptId);
    }

    private static string SelfRollKey(Guid gameId, Guid playerId) => $"{gameId}:{playerId}";
}
