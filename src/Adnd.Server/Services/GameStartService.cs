using Adnd.Server.Data;
using Adnd.Server.Models;
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
    IAgentBus agentBus,
    ILogger<GameStartService> logger) : IGameStartService
{
    public async Task StartGameAsync(Guid gameId, CancellationToken ct = default)
    {
        logger.LogInformation("Starting game {GameId}", gameId);

        // Create (or get) the initial session
        var session = await sessions.GetOrCreateCurrentSessionAsync(gameId);

        // Seed default prompt templates (S3) if none exist for this game's system
        await SeedPromptTemplatesAsync(gameId, ct);

        // Generate initial plot threads
        if (!await plotWeaver.HasInitialThreadsAsync(gameId, ct))
        {
            logger.LogInformation("Generating initial plot threads for game {GameId}", gameId);
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

        // Queue opening narration via AgentBus
        var call = new AgentCall
        {
            GameId = gameId,
            SessionId = session.Id,
            FromAgent = AgentType.System,
            ToAgent = AgentType.GM,
            Action = AgentAction.OpenNarrative,
            Input = "Generate the opening narration for this game."
        };

        await agentBus.SendCallAsync(call);
        logger.LogInformation("Opening narration queued for game {GameId}", gameId);
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
