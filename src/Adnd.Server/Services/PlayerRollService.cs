using Adnd.Server.Data;
using Adnd.Server.Hubs;
using Adnd.Server.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Services;

/// <param name="PlayerId">Null for a roll the GM makes itself, which has no character behind
/// it and therefore never produces a reroll offer.</param>
/// <param name="IsSecret">Visible to the roller and the game's creator only. The AI narrator
/// still sees it — see <c>MessageVisibility</c>.</param>
/// <param name="PostMessage">False when the caller is going to resolve an existing prompt
/// message into this result instead. Posting here as well would show the roll twice.</param>
public record PlayerRollRequest(
    Guid GameId,
    Guid SessionId,
    Guid? PlayerId,
    string Formula,
    int? Dc = null,
    string? Reason = null,
    bool IsSecret = false,
    string MessageType = "DiceRoll",
    bool PostMessage = true);

public record PlayerRollResult(
    string Content,
    int Total,
    int? Dc,
    bool? Success,
    Guid? CharacterId,
    IReadOnlyList<RerollOption> RerollOptions);

public interface IPlayerRollService
{
    /// <summary>Rolls, records it as a chat message, broadcasts it, and reports any rerolls
    /// the roller's character could still apply to it.</summary>
    Task<PlayerRollResult> RollAsync(PlayerRollRequest request, CancellationToken ct = default);

    /// <summary>
    /// Spends the named ability and rolls again. The replacement is announced to the table
    /// naming the ability, because a number silently changing is indistinguishable from a bug.
    /// Returns null if the ability isn't available — a stale or replayed accept.
    /// </summary>
    Task<PlayerRollResult?> RerollAsync(PlayerRollRequest request, Guid characterId, string featureId, CancellationToken ct = default);
}

/// <summary>
/// Every roll attributed to a player goes through here: the hub's own RollDice, the GM's
/// requestPlayerRoll tool, and the reroll that may follow either. Keeping them together is
/// what lets "does this character have a reroll for that?" be answered in one place instead
/// of at each call site, which is how it would come to be answered in only some of them.
/// </summary>
public class PlayerRollService(
    AppDbContext db,
    IDiceEngine diceEngine,
    IRerollService rerolls,
    IFeatureCatalogue catalogue,
    IHubContext<GameHub> hub) : IPlayerRollService
{
    public async Task<PlayerRollResult> RollAsync(PlayerRollRequest request, CancellationToken ct = default)
    {
        var roll = diceEngine.Roll(request.Formula);
        var (content, success) = RollFormatting.Describe(request.Formula, roll, request.Dc, request.Reason);

        if (request.PostMessage)
            await PersistAndBroadcastAsync(request, content, roll, success, ct);

        var characterId = await ResolveCharacterIdAsync(request.PlayerId, ct);
        var options = characterId is { } cid
            ? await rerolls.GetOptionsAsync(cid, roll, request.Dc, ct)
            : [];

        return new PlayerRollResult(content, roll.Total, request.Dc, success, characterId, options);
    }

    public async Task<PlayerRollResult?> RerollAsync(PlayerRollRequest request, Guid characterId, string featureId, CancellationToken ct = default)
    {
        if (!await rerolls.TryConsumeAsync(characterId, featureId, ct))
            return null;

        var featureName = catalogue.GetById(featureId)?.Name ?? featureId;
        var roll = diceEngine.Roll(request.Formula);
        var (described, success) = RollFormatting.Describe(request.Formula, roll, request.Dc, request.Reason);
        var content = $"{featureName} — reroll. {described}";

        if (request.PostMessage)
            await PersistAndBroadcastAsync(request, content, roll, success, ct);

        // No second offer: the abilities modelled here all say the new roll stands, and an
        // ability that could chain would need its own rule rather than this fall-through.
        return new PlayerRollResult(content, roll.Total, request.Dc, success, characterId, []);
    }

    private async Task PersistAndBroadcastAsync(
        PlayerRollRequest request, string content, DiceResult roll, bool? success, CancellationToken ct)
    {
        var msg = new Message
        {
            SessionId = request.SessionId,
            PlayerId = request.PlayerId,
            Content = content,
            Type = request.MessageType,
            IsSecret = request.IsSecret,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Messages.Add(msg);
        await db.SaveChangesAsync(ct);

        var metadata = new
        {
            formula = roll.Formula,
            total = roll.Total,
            breakdown = roll.Breakdown,
            dc = request.Dc,
            success,
            isSecret = request.IsSecret
        };
        var dto = new MessageDto(msg.Id, request.SessionId, request.PlayerId, content,
            request.MessageType, false, msg.CreatedAt, metadata);

        if (!request.IsSecret)
        {
            await hub.Clients.Group(request.GameId.ToString()).SendAsync("NewMessage", dto, ct);
            return;
        }

        // "Secret" means secret from the other players, not from the GM — so it goes to the
        // roller and to the game's creator, and to nobody else. Addressing users rather than
        // the group is what makes that true for the live push; MessagesController.IsSecret
        // is what makes it true on reload.
        foreach (var userId in await SecretAudienceAsync(request.GameId, request.PlayerId, ct))
            await hub.Clients.User(userId).SendAsync("NewMessage", dto, ct);
    }

    private async Task<List<string>> SecretAudienceAsync(Guid gameId, Guid? playerId, CancellationToken ct)
    {
        var creatorId = await db.Games
            .Where(g => g.Id == gameId)
            .Select(g => (Guid?)g.CreatorId)
            .FirstOrDefaultAsync(ct);

        var rollerUserId = playerId is { } pid
            ? await db.Players.Where(p => p.Id == pid).Select(p => (Guid?)p.UserId).FirstOrDefaultAsync(ct)
            : null;

        return new[] { creatorId, rollerUserId }
            .OfType<Guid>()
            .Distinct()
            .Select(id => id.ToString())
            .ToList();
    }

    private async Task<Guid?> ResolveCharacterIdAsync(Guid? playerId, CancellationToken ct)
        => playerId is not { } pid
            ? null
            : await db.Characters
                .Where(c => c.PlayerId == pid)
                .Select(c => (Guid?)c.Id)
                .FirstOrDefaultAsync(ct);
}

/// <summary>
/// One phrasing for every roll the table sees. Without a stated target, "Attempting to force
/// open the door: 17" leaves the player unable to tell whether that succeeded.
/// </summary>
public static class RollFormatting
{
    public static (string Content, bool? Success) Describe(string formula, DiceResult result, int? dc, string? reason)
    {
        bool? success = dc.HasValue ? result.Total >= dc.Value : null;
        var prefix = string.IsNullOrEmpty(reason) ? "" : $"{reason} — ";
        var suffix = dc.HasValue
            ? $" vs DC {dc}: {result.Breakdown} — {(success!.Value ? "Success" : "Failure")}"
            : $": {result.Breakdown}";
        return ($"{prefix}Rolled {formula}{suffix}", success);
    }
}
