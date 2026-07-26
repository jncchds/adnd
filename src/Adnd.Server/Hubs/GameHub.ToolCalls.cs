namespace Adnd.Server.Hubs;

public partial class GameHub
{
    public async Task ConfirmPlayerRoll(Guid gameId, Guid agentCallId, int rollResult)
        => await BroadcastToGameAsync(gameId, "PlayerRollConfirmed", new { agentCallId, rollResult });

    public async Task DeclinePlayerRoll(Guid gameId, Guid agentCallId)
        => await BroadcastToGameAsync(gameId, "PlayerRollDeclined", new { agentCallId });

    public async Task ConfirmToolCall(Guid gameId, Guid agentCallId)
        => await BroadcastToGameAsync(gameId, "ToolCallConfirmed", new { agentCallId });
}
