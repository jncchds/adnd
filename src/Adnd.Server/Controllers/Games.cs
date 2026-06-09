using Microsoft.AspNetCore.Mvc;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Adnd.Server.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;

namespace Adnd.Server.Controllers;

public partial class AdminController
{
    // ==================== Game Management ====================

    [HttpPost("games/{gameId}/start")]
    public async Task<IActionResult> StartGame(Guid gameId)
    {
        var userId = _userIdProvider.GetCurrentUserId();
        var game = await _context.Games
            .Include(g => g.LLMPreset)
            .FirstOrDefaultAsync(g => g.Id == gameId);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (game.CreatorId != userId) return Forbid();

        if (game.LLMPresetId == null)
            return BadRequest(new { error = "Cannot start: no LLM preset configured. Set one in game creation." });

        game.Status = GameStatus.Active;
        game.StartedAt = DateTime.UtcNow;
        game.GMStatus = GMStatus.Running;
        await _context.SaveChangesAsync();

        // Generate initial plot threads from the plot seed
        try
        {
            var threads = await _plotWeaver.GenerateInitialThreadsAsync(
                gameId,
                game.PlotSeed ?? "An epic adventure",
                game.GameParameters ?? "",
                game.SystemId,
                game.LLMPresetId);
            _logger.LogInformation("Generated {Count} initial plot threads for game {GameId}", threads.Count, gameId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate initial plot threads for game {GameId}", gameId);
            // Don't fail the game start if plot thread generation fails
        }

        // Generate opening narrative scene
        string? openingNarrative = null;
        try
        {
            var provider = _providerRegistry.GetProvider(game.LLMPreset.ProviderType);
            if (provider != null)
            {
                var systemPrompt = $"You are the Game Master for a TTRPG session. " +
                    $"Game system: {game.SystemId}. " +
                    $"Plot seed: {game.PlotSeed ?? "None"}. " +
                    $"Game parameters: {game.GameParameters ?? "None"}. " +
                    $"Your task is to write an immersive opening scene that introduces the world, " +
                    $"sets the tone, and invites the players into the story. Be vivid and engaging. " +
                    $"Write in second person to immerse the players. Limit to 2-4 paragraphs.";

                var userPrompt = "Generate the opening narrative for this game session. " +
                    "Write it as if you are describing the scene to the players at the table.";

                var result = await provider.CompleteAsync(systemPrompt, userPrompt, new LLMOptions
                {
                    Temperature = 0.85f,
                    MaxTokens = 2048
                });
                openingNarrative = result.Trim();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate opening narrative for game {GameId}", gameId);
            openingNarrative = "The adventure begins...";
        }

        // Create the opening message in the DB and broadcast it
        if (!string.IsNullOrEmpty(openingNarrative))
        {
            // Find or create the first active session
            var session = await _context.GameSessions
                .FirstOrDefaultAsync(s => s.GameId == gameId && s.EndedAt == null);
            if (session == null)
            {
                session = new GameSession
                {
                    GameId = gameId,
                    Title = "Session 1",
                    Description = "Opening session",
                    StartedAt = DateTime.UtcNow
                };
                _context.GameSessions.Add(session);
                await _context.SaveChangesAsync();
            }

            var message = new Message
            {
                SessionId = session.Id,
                PlayerId = null,
                Content = openingNarrative,
                Type = Adnd.Server.Models.MessageType.GM,
                IsOOC = false,
                Metadata = default,
                CreatedAt = DateTime.UtcNow
            };
            _context.Messages.Add(message);
            await _context.SaveChangesAsync();

            // Broadcast the opening message to all players in the game
            await _hubContext.Clients.Group(gameId.ToString()).SendAsync("NewMessage", new
            {
                Id = message.Id,
                SessionId = message.SessionId,
                PlayerId = (Guid?)null,
                Content = message.Content,
                Type = (int)message.Type,
                IsOOC = false,
                WhisperFromId = (Guid?)null,
                WhisperToId = (Guid?)null,
                WhisperTarget = (string?)null,
                CreatedAt = message.CreatedAt
            });
        }

        // Publish game started event → triggers GameAgent activation
        await _mediator.Publish(new GameStarted(gameId, userId));

        return Ok(new { game.Id, game.Status, game.StartedAt });
    }

    [HttpPost("games/{gameId}/archive")]
    public async Task<IActionResult> ArchiveGame(Guid gameId)
    {
        var userId = _userIdProvider.GetCurrentUserId();
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });
        if (game.CreatorId != userId) return Forbid();

        game.Status = GameStatus.Archived;
        game.EndedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Publish game archived event → triggers GameAgent pause
        await _mediator.Publish(new GameArchived(gameId));

        return Ok(new { game.Id, game.Status, game.EndedAt });
    }

}
