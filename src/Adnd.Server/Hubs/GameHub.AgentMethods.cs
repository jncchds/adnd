using Adnd.Server.Dtos;
using Adnd.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Hubs;

public partial class GameHub
{
    public async Task TriggerNarrate(Guid gameId, string prompt)
    {
        // Queues a billable LLM call against the game owner's API key.
        await RequireMemberAsync(gameId);
        var session = await ResolveGameSessionAsync(gameId);
        var systemPrompt = await BuildNarrateSystemPromptAsync(gameId);
        var options = new GMDispatchOptions(
            SystemPrompt: systemPrompt,
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

    private async Task<string> BuildNarrateSystemPromptAsync(Guid gameId)
    {
        var game = await db.Games.Include(g => g.Players).ThenInclude(p => p.User)
            .FirstOrDefaultAsync(g => g.Id == gameId);
        if (game == null) return "You are the AI Game Master for an ongoing TTRPG session.";

        var plots = await db.PlotThreads
            .Where(p => p.GameId == gameId && !p.IsDeleted && p.Status == PlotThreadStatus.Active)
            .OrderByDescending(p => p.Momentum)
            .Take(4)
            .ToListAsync();

        var characters = await db.Characters
            .Where(c => c.Player.GameId == gameId && !c.IsDeleted)
            .Include(c => c.Player)
            .ToListAsync();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("You are the AI Game Master for an ongoing TTRPG session. Respond in character at all times.");
        sb.AppendLine("CRITICAL: Never break the fourth wall. Never mention context retrieval, system limitations, or technical issues. If you lack information, improvise based on established fiction.");
        sb.AppendLine();
        sb.AppendLine($"Game: {game.Name} | System: {game.SystemId}");

        if (!string.IsNullOrEmpty(game.PlotSeed))
        {
            sb.AppendLine($"Campaign seed: {game.PlotSeed}");
            sb.AppendLine();
        }

        if (plots.Count > 0)
        {
            sb.AppendLine("Active plot threads:");
            foreach (var p in plots)
                sb.AppendLine($"- [{p.Category}] {p.Title}: {p.Description}");
            sb.AppendLine();
        }

        if (characters.Count > 0)
        {
            sb.AppendLine("Player characters:");
            foreach (var c in characters)
                sb.AppendLine($"- {c.Name} (Level {c.Level} {c.Class}, HP {c.CurrentHP}/{c.MaxHP})");
            sb.AppendLine();
        }

        sb.AppendLine("Narrate the scene vividly. Use the available tools (rollDice, skillCheck, startCombat, etc.) when appropriate. Keep responses concise and end with an open question or clear call to action.");
        return sb.ToString();
    }

    public async Task TriggerSuggest(Guid gameId, string prompt)
    {
        await RequireMemberAsync(gameId);
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
    {
        await RequireMemberAsync(gameId);
        return await agentBus.GetGMStatusAsync(gameId);
    }

    // Halting the GM is a denial of service against everyone else at the table — creator only.
    public async Task PauseGM(Guid gameId)
    {
        await RequireCreatorAsync(gameId);
        await agentBus.PauseGMAsync(gameId);
        await BroadcastToGameAsync(gameId, "GMStatusChanged", new GMStatusDto(gameId, "Paused", null));
    }

    public async Task ResumeGM(Guid gameId)
    {
        await RequireCreatorAsync(gameId);
        await agentBus.ResumeGMAsync(gameId);
        await BroadcastToGameAsync(gameId, "GMStatusChanged", new GMStatusDto(gameId, "Running", null));
    }
}
