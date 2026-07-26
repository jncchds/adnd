using Adnd.Server.Data;
using Adnd.Server.Events;
using Adnd.Server.Models;

namespace Adnd.Server.Services;

public interface IAgentBus
{
    Task<AgentCall> SendCallAsync(AgentCall call);
    Task<string?> GetGMStatusAsync(Guid gameId);
    Task PauseGMAsync(Guid gameId);
    Task ResumeGMAsync(Guid gameId);
    Task SendSwayAsync(Guid gameId, string direction, int intensity, string content);
}

public class AgentBus(AppDbContext db, IEventBus eventBus) : IAgentBus
{
    public async Task<AgentCall> SendCallAsync(AgentCall call)
    {
        call.Status = AgentCallStatus.Pending;
        db.AgentCalls.Add(call);
        await db.SaveChangesAsync();
        await eventBus.PublishAsync(new AgentCallQueued(call.Id, call.GameId));
        return call;
    }

    public async Task<string?> GetGMStatusAsync(Guid gameId)
    {
        var game = await db.Games.FindAsync(gameId);
        return game?.GMStatus.ToString();
    }

    public async Task PauseGMAsync(Guid gameId)
    {
        var game = await db.Games.FindAsync(gameId);
        if (game != null) { game.GMStatus = GMStatus.Paused; await db.SaveChangesAsync(); }
    }

    public async Task ResumeGMAsync(Guid gameId)
    {
        var game = await db.Games.FindAsync(gameId);
        if (game != null) { game.GMStatus = GMStatus.Running; await db.SaveChangesAsync(); }
    }

    public async Task SendSwayAsync(Guid gameId, string direction, int intensity, string content)
        => await eventBus.PublishAsync(new StorySwayed(gameId, direction, intensity, content));
}
