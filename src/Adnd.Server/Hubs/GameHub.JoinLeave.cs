using Adnd.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Hubs;

public partial class GameHub
{
    public async Task JoinGameGroup(Guid gameId)
    {
        var userId = CurrentUserId;
        var player = await db.Players.Include(p => p.User)
            .FirstOrDefaultAsync(p => p.GameId == gameId && p.UserId == userId);
        if (player == null) { await Clients.Caller.SendCoreAsync("Error", ["Not a member of this game."]); return; }

        await Groups.AddToGroupAsync(Context.ConnectionId, gameId.ToString());

        player.IsConnected = true;
        await db.SaveChangesAsync();

        var dto = new PlayerDto(player.Id, gameId, userId, player.CharacterName, player.Role.ToString(), true);
        await BroadcastToGameAsync(gameId, "PlayerReconnected", dto);

        // A plain SignalR broadcast has no memory: joining mid-generation (e.g. already
        // sitting in chat when the GM clicks "Start Game", or navigating in right after)
        // means every GMActivity push that already fired was missed, showing a static
        // "GM Active" for the rest of the ~30-60s game-start pipeline. Catch this caller up
        // on whatever's in progress right now.
        // And when nothing is in progress, say so: a client that was disconnected while the
        // turn ended would otherwise come back still showing the chip it left with.
        await Clients.Caller.SendCoreAsync("GMActivity",
            [gmActivity.GetCurrent(gameId) ?? new GMActivityDto(gameId, nameof(SagaStep.Completed), null)]);

        // Same for a fight in progress: combat only ever arrived as live pushes, so a reload
        // mid-fight lost the panel until something happened to change it.
        if (await combats.GetActiveAsync(gameId) is { } combat)
            await Clients.Caller.SendCoreAsync("CombatUpdated", [combats.ToDto(combat)]);
    }
}
