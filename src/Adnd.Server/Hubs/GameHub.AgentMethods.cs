using Adnd.Server.Dtos;
using Adnd.Server.Models;

namespace Adnd.Server.Hubs;

public partial class GameHub
{
    public async Task TriggerNarrate(Guid gameId, string prompt)
    {
        var session = await ResolveGameSessionAsync(gameId);
        var options = new GMDispatchOptions(
            SystemPrompt: "You are the AI Game Master for an ongoing TTRPG session.",
            UserPrompt: prompt);
        var call = new AgentCall
        {
            GameId = gameId,
            SessionId = session.Id,
            FromAgent = AgentType.Player,
            ToAgent = AgentType.GM,
            Action = AgentAction.Narrate,
            Input = System.Text.Json.JsonSerializer.Serialize(options)
        };
        await agentBus.SendCallAsync(call);
    }

    public async Task TriggerSuggest(Guid gameId, string prompt)
    {
        var session = await ResolveGameSessionAsync(gameId);
        var options = new GMDispatchOptions("You are an AI GM assistant.", prompt);
        var call = new AgentCall
        {
            GameId = gameId,
            SessionId = session.Id,
            FromAgent = AgentType.Player,
            ToAgent = AgentType.GM,
            Action = AgentAction.Suggest,
            Input = System.Text.Json.JsonSerializer.Serialize(options)
        };
        await agentBus.SendCallAsync(call);
    }

    public async Task<string?> GetGMStatus(Guid gameId)
        => await agentBus.GetGMStatusAsync(gameId);

    public async Task PauseGM(Guid gameId)
    {
        await agentBus.PauseGMAsync(gameId);
        await BroadcastToGameAsync(gameId, "GMStatusChanged", new GMStatusDto(gameId, "Paused", null));
    }

    public async Task ResumeGM(Guid gameId)
    {
        await agentBus.ResumeGMAsync(gameId);
        await BroadcastToGameAsync(gameId, "GMStatusChanged", new GMStatusDto(gameId, "Running", null));
    }
}
