using Adnd.Server.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Hubs;

public partial class GameHub
{
    /// <summary>
    /// Restores the caller's per-rest abilities. Without this, every limited reroll in the
    /// game was strictly one-way: a character spent their three Lucky points once and never
    /// saw the offer again for the rest of the campaign.
    /// </summary>
    public async Task TakeRest(Guid gameId, string restType)
    {
        var player = await RequireMemberAsync(gameId);
        var session = await ResolveGameSessionAsync(gameId);

        if (!Enum.TryParse<RestPeriod>(restType, ignoreCase: true, out var period))
            throw new HubException("Rest type must be ShortRest or LongRest.");

        var character = await db.Characters.FirstOrDefaultAsync(c => c.PlayerId == player.Id)
            ?? throw new HubException("You have no character to rest.");

        var restored = await rerolls.RestoreOnRestAsync(character.Id, period);

        var label = period == RestPeriod.LongRest ? "long rest" : "short rest";
        var content = restored > 0
            ? $"{character.Name} takes a {label}. {restored} ability {(restored == 1 ? "use is" : "uses are")} restored."
            : $"{character.Name} takes a {label}.";

        var msg = await PersistGameEventAsync(session.Id, content, "System", player.Id);

        var dto = new MessageDto(msg.Id, session.Id, player.Id, content, "System", false,
            msg.CreatedAt, new { restType = period.ToString(), restored });
        await BroadcastToGameAsync(gameId, "NewMessage", dto);

        // The sheet's remaining-uses counts are now stale on every client showing it.
        await BroadcastToGameAsync(gameId, "CharacterUpdated", new { characterId = character.Id });
    }
}
