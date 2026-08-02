using Adnd.Server.Data;
using Adnd.Server.Hubs;
using Adnd.Server.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Adnd.Server.Services;

public interface IGameStartService
{
    Task StartGameAsync(Guid gameId, CancellationToken ct = default);
}

public class GameStartService(
    AppDbContext db,
    ISessionManagementService sessions,
    IPlotWeaver plotWeaver,
    INarrativeGenerationFactory narrativeFactory,
    IHubContext<GameHub> hub,
    IGmActivityBroadcaster activity,
    ILogger<GameStartService> logger) : IGameStartService
{
    public async Task StartGameAsync(Guid gameId, CancellationToken ct = default)
    {
        logger.LogInformation("Starting game {GameId}", gameId);

        // Create (or get) the initial session
        var session = await sessions.GetOrCreateCurrentSessionAsync(gameId);

        // This path generates the opening narration directly rather than going through
        // AgentSaga, so it never publishes GameNarrationStarted — the event that normally
        // flips Game.Status from Starting to Active. Without this, a freshly-started game
        // stays stuck at Starting forever (no admin-dashboard button matches that status).
        var game = await db.Games.FindAsync([gameId], ct);
        if (game is not null && game.Status == GameStatus.Starting)
        {
            game.Status = GameStatus.Active;
            await db.SaveChangesAsync(ct);
        }

        // Seed default prompt templates (S3) if none exist for this game's system
        await SeedPromptTemplatesAsync(gameId, ct);

        // Generate initial plot threads
        if (!await plotWeaver.HasInitialThreadsAsync(gameId, ct))
        {
            logger.LogInformation("Generating initial plot threads for game {GameId}", gameId);
            await activity.BroadcastAsync(gameId, "GeneratingPlot", ct: ct);
            await plotWeaver.ReviewAndAdaptAsync(gameId, ct);
        }

        // S4: If this game has prior sessions, generate a recap first
        var priorSessions = await db.GameSessions
            .Where(s => s.GameId == gameId && s.Id != session.Id)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (priorSessions is not null)
        {
            logger.LogInformation("Generating recap for prior session {SessionId} in game {GameId}", priorSessions.Id, gameId);
            await activity.BroadcastAsync(gameId, "GeneratingRecap", ct: ct);
            var recap = await narrativeFactory.GenerateSessionRecapAsync(gameId, priorSessions.Id, ct);
            if (!string.IsNullOrEmpty(recap))
            {
                db.Messages.Add(new Message
                {
                    SessionId = session.Id,
                    Content = $"**Previously...** {recap}",
                    Type = "GM",
                    CreatedAt = DateTimeOffset.UtcNow
                });
                await db.SaveChangesAsync(ct);
            }
        }

        // Generate and broadcast opening narration directly
        logger.LogInformation("Generating opening narration for game {GameId}", gameId);
        await activity.BroadcastAsync(gameId, "GeneratingNarration", ct: ct);
        var narration = await narrativeFactory.GenerateOpeningNarrationAsync(gameId, ct);
        if (!string.IsNullOrEmpty(narration))
        {
            var msg = new Message
            {
                SessionId = session.Id,
                Content = narration,
                Type = "GM",
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.Messages.Add(msg);
            await db.SaveChangesAsync(ct);

            var dto = new MessageDto(msg.Id, msg.SessionId, null, msg.Content, msg.Type, false, msg.CreatedAt, null);
            await hub.Clients.Group(gameId.ToString()).SendAsync("NewMessage", dto, ct);
        }

        // Always broadcast Completed, success or not. The client used to clear its activity
        // chip purely by observing a GM NewMessage arrive, which worked as long as nothing
        // remembered activity state server-side. Now that IGmActivityBroadcaster caches the
        // last activity per game (so a client joining mid-generation can catch up), leaving
        // this unbroadcast on the success path meant the cache stayed stuck on
        // "GeneratingNarration" forever, and anyone who reconnected afterwards — e.g. a player
        // navigating away to create a character and back — got replayed that stale "Writing
        // the opening scene…" chip despite the turn having finished ages ago.
        await activity.BroadcastAsync(gameId, "Completed", ct: ct);
    }

    private async Task SeedPromptTemplatesAsync(Guid gameId, CancellationToken ct)
    {
        var game = await db.Games.FindAsync([gameId], ct);
        if (game is null) return;

        // Only seed if no default template exists for this system
        var exists = await db.PromptTemplates
            .AnyAsync(t => t.IsDefault && t.Type == game.SystemId && t.GameId == null, ct);

        if (exists) return;

        var templates = GetDefaultTemplates();
        foreach (var template in templates)
            db.PromptTemplates.Add(template);

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded default prompt templates");
    }

    private static List<PromptTemplate> GetDefaultTemplates() =>
    [
        new PromptTemplate
        {
            Name = "D&D 5e GM",
            Type = "dnd5e",
            IsDefault = true,
            Content = "You are a skilled Dungeon Master running a D&D 5th Edition campaign. Your narration is vivid and immersive. You balance tactical challenge with dramatic storytelling, respect the rules while knowing when to bend them for the story, and ensure every player has meaningful moments. Use markdown for formatting. Keep responses focused and under 300 words unless describing a major scene."
        },
        new PromptTemplate
        {
            Name = "Pathfinder 2e GM",
            Type = "pf2e",
            IsDefault = true,
            Content = "You are a skilled Game Master running a Pathfinder 2e campaign. You embrace the rich action economy and tactical depth of the system. Your narration is gritty and heroic, your encounters challenging but fair. Emphasize player agency and the consequences of their choices. Use markdown for formatting."
        },
        new PromptTemplate
        {
            Name = "Call of Cthulhu GM",
            Type = "coc7e",
            IsDefault = true,
            Content = "You are a Keeper of Arcane Lore running a Call of Cthulhu 7th Edition campaign. The cosmos is vast and indifferent. Your atmosphere drips with dread and your narration relentless. The horrors are real and the investigators' sanity is precious. Never pull punches — survival is not guaranteed. Use markdown for formatting."
        },
        new PromptTemplate
        {
            Name = "Generic GM",
            Type = "custom",
            IsDefault = true,
            Content = "You are a skilled Game Master running a tabletop RPG campaign. Your narration is vivid and immersive, your rulings fair and exciting. Ensure every player has meaningful moments and their choices have real consequences. Use markdown for formatting. Keep responses focused."
        }
    ];
}
