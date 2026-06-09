using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Adnd.Server.Events;
using MediatR;

namespace Adnd.Server.Hubs;

public partial class GameHub
{
    // ==================== Chat Messages ====================

    /// <summary>
    /// Send an in-game public message (part of game narrative).
    /// </summary>
    public async Task SendMessage(Guid sessionId, string content)
    {
        var session = await _context.GameSessions.FindAsync(sessionId);
        if (session == null)
        {
            await Clients.Caller.SendAsync("Error", new { message = "Session not found." });
            return;
        }

        var userId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var uid))
        {
            await Clients.Caller.SendAsync("Error", new { message = "Authentication required." });
            return;
        }

        var player = await _context.Players
            .FirstOrDefaultAsync(p => p.GameId == session.GameId && p.UserId == uid && p.Status == PlayerStatus.Active);

        if (player == null)
        {
            await Clients.Caller.SendAsync("Error", new { message = "You are not an active player in this game." });
            return;
        }

        var message = new Message
        {
            SessionId = sessionId,
            PlayerId = player.Id,
            Content = content,
            Type = Adnd.Server.Models.MessageType.InGamePublic,
            IsOOC = false,
            Metadata = default,
            CreatedAt = DateTime.UtcNow
        };

        _context.Messages.Add(message);
        await _context.SaveChangesAsync();

        // Generate embedding for narrative-influencing messages
        await EmbedMessageAsync(session.GameId, message.Id, message.Content);

        // Publish event for game agent processing (in-game only)
        await _mediator.Publish(new MessageSent(
            session.GameId, sessionId, player.Id, content, Adnd.Server.Events.MessageType.InGamePublic, null, false));

        await Clients.Group(session.GameId.ToString()).SendAsync("NewMessage", new
        {
            message.Id,
            message.SessionId,
            message.PlayerId,
            message.Content,
            message.Type,
            message.Metadata,
            message.IsOOC,
            WhisperFromId = (Guid?)null,
            WhisperToId = (Guid?)null,
            WhisperTarget = (string?)null,
            message.CreatedAt
        });
    }

    /// <summary>
    /// Send an in-game whisper (to GM — adds to GM knowledge).
    /// </summary>
    public async Task SendInGameWhisper(Guid sessionId, string content)
    {
        var session = await _context.GameSessions.FindAsync(sessionId);
        if (session == null)
        {
            await Clients.Caller.SendAsync("Error", new { message = "Session not found." });
            return;
        }

        var userId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var uid))
        {
            await Clients.Caller.SendAsync("Error", new { message = "Authentication required." });
            return;
        }

        var player = await _context.Players
            .FirstOrDefaultAsync(p => p.GameId == session.GameId && p.UserId == uid && p.Status == PlayerStatus.Active);

        if (player == null)
        {
            await Clients.Caller.SendAsync("Error", new { message = "You are not an active player in this game." });
            return;
        }

        var message = new Message
        {
            SessionId = sessionId,
            PlayerId = player.Id,
            Content = content,
            Type = Adnd.Server.Models.MessageType.InGameWhisper,
            IsOOC = false,
            WhisperFromId = player.Id,
            WhisperTarget = "gm",
            Metadata = default,
            CreatedAt = DateTime.UtcNow
        };

        _context.Messages.Add(message);
        await _context.SaveChangesAsync();

        // Generate embedding for in-game whisper (influences GM knowledge)
        await EmbedMessageAsync(session.GameId, message.Id, message.Content);

        // Create whisper record for GM targeting
        var whisper = await _whisperService.SendWhisperAsync(
            session.GameId, sessionId, player.Id, "gm",
            Adnd.Server.Models.WhisperType.InGamePlayerToGM, content);

        // Publish whisper event (not message event — this is not public narrative)
        await _mediator.Publish(new WhisperSent(
            session.GameId, player.Id, "gm", content, Adnd.Server.Events.WhisperType.InGamePlayerToGM));

        // Send to sender (confirmation)
        await Clients.Caller.SendAsync("NewWhisper", new
        {
            whisper.Id,
            FromPlayerId = whisper.FromPlayerId,
            FromCharacter = player.CharacterName,
            FromRole = player.Role,
            Content = whisper.Content,
            Type = whisper.Type,
            Targets = whisper.Targets,
            CreatedAt = whisper.CreatedAt,
            IsSent = true
        });

        // Send to GM (Creator)
        var gmPlayer = await _context.Players
            .FirstOrDefaultAsync(p => p.GameId == session.GameId && p.Role == PlayerRole.Creator && p.Status == PlayerStatus.Active);

        if (gmPlayer != null)
        {
            var gmConnectionId = GetConnectionIdForPlayer(gmPlayer.Id);
            if (gmConnectionId != null)
            {
                await Clients.Client(gmConnectionId).SendAsync("NewWhisper", new
                {
                    whisper.Id,
                    FromPlayerId = whisper.FromPlayerId,
                    FromCharacter = player.CharacterName,
                    FromRole = player.Role,
                    Content = whisper.Content,
                    Type = whisper.Type,
                    Targets = whisper.Targets,
                    CreatedAt = whisper.CreatedAt,
                    IsReceived = true
                });
            }
        }

        _logger.LogInformation("In-game whisper from {CharacterName} to GM in game {GameId}",
            player.CharacterName, session.GameId);
    }

    /// <summary>
    /// Send an OOC public message (never influences narrative).
    /// </summary>
    public async Task SendOOCMessage(Guid sessionId, string content)
    {
        var session = await _context.GameSessions.FindAsync(sessionId);
        if (session == null)
        {
            await Clients.Caller.SendAsync("Error", new { message = "Session not found." });
            return;
        }

        var userId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var uid))
        {
            await Clients.Caller.SendAsync("Error", new { message = "Authentication required." });
            return;
        }

        var player = await _context.Players
            .FirstOrDefaultAsync(p => p.GameId == session.GameId && p.UserId == uid && p.Status == PlayerStatus.Active);

        if (player == null)
        {
            await Clients.Caller.SendAsync("Error", new { message = "You are not an active player in this game." });
            return;
        }

        var message = new Message
        {
            SessionId = sessionId,
            PlayerId = player.Id,
            Content = content,
            Type = Adnd.Server.Models.MessageType.OOCPublic,
            IsOOC = true,
            Metadata = default,
            CreatedAt = DateTime.UtcNow
        };

        _context.Messages.Add(message);
        await _context.SaveChangesAsync();

        // Publish OOC event — NOT processed by GameAgent
        await _mediator.Publish(new OOCMessageSent(
            session.GameId, sessionId, player.Id, content, "public"));

        await Clients.Group(session.GameId.ToString()).SendAsync("NewOOCMessage", new
        {
            message.Id,
            message.SessionId,
            message.PlayerId,
            message.Content,
            message.Type,
            message.IsOOC,
            message.CreatedAt
        });
    }

    /// <summary>
    /// Send an OOC whisper from a player to the GM (for clarification).
    /// GM responds with OOCWhisper.
    /// </summary>
    public async Task SendOOCWhisper(Guid sessionId, string content)
    {
        var session = await _context.GameSessions.FindAsync(sessionId);
        if (session == null)
        {
            await Clients.Caller.SendAsync("Error", new { message = "Session not found." });
            return;
        }

        var userId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var uid))
        {
            await Clients.Caller.SendAsync("Error", new { message = "Authentication required." });
            return;
        }

        var player = await _context.Players
            .FirstOrDefaultAsync(p => p.GameId == session.GameId && p.UserId == uid && p.Status == PlayerStatus.Active);

        if (player == null)
        {
            await Clients.Caller.SendAsync("Error", new { message = "You are not an active player in this game." });
            return;
        }

        var message = new Message
        {
            SessionId = sessionId,
            PlayerId = player.Id,
            Content = content,
            Type = Adnd.Server.Models.MessageType.OOCWhisper,
            IsOOC = true,
            WhisperFromId = player.Id,
            WhisperTarget = "gm",
            Metadata = default,
            CreatedAt = DateTime.UtcNow
        };

        _context.Messages.Add(message);
        await _context.SaveChangesAsync();

        // Create whisper record
        var whisper = await _whisperService.SendWhisperAsync(
            session.GameId, sessionId, player.Id, "gm",
            Adnd.Server.Models.WhisperType.OOCPlayerToGM, content);

        // Publish OOC whisper event — NOT processed by GameAgent
        await _mediator.Publish(new OOCWhisperSent(
            session.GameId, player.Id, "gm", content));

        // Send to sender (confirmation)
        await Clients.Caller.SendAsync("NewOOCWhisper", new
        {
            whisper.Id,
            FromPlayerId = whisper.FromPlayerId,
            FromCharacter = player.CharacterName,
            FromRole = player.Role,
            Content = whisper.Content,
            Type = whisper.Type,
            Targets = whisper.Targets,
            CreatedAt = whisper.CreatedAt,
            IsSent = true
        });

        // Send to GM (Creator)
        var gmPlayer = await _context.Players
            .FirstOrDefaultAsync(p => p.GameId == session.GameId && p.Role == PlayerRole.Creator && p.Status == PlayerStatus.Active);

        if (gmPlayer != null)
        {
            var gmConnectionId = GetConnectionIdForPlayer(gmPlayer.Id);
            if (gmConnectionId != null)
            {
                await Clients.Client(gmConnectionId).SendAsync("NewOOCWhisper", new
                {
                    whisper.Id,
                    FromPlayerId = whisper.FromPlayerId,
                    FromCharacter = player.CharacterName,
                    FromRole = player.Role,
                    Content = whisper.Content,
                    Type = whisper.Type,
                    Targets = whisper.Targets,
                    CreatedAt = whisper.CreatedAt,
                    IsReceived = true
                });
            }
        }

        _logger.LogInformation("OOC whisper from {CharacterName} to GM in game {GameId}",
            player.CharacterName, session.GameId);
    }

    /// <summary>
    /// GM sends an OOC whisper to a player (e.g., clarifying rules).
    /// </summary>
    public async Task SendOOCWhisperToPlayer(Guid targetPlayerId, string content)
    {
        var userId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var uid))
        {
            await Clients.Caller.SendAsync("Error", new { message = "Authentication required." });
            return;
        }

        var gmPlayer = await _context.Players
            .FirstOrDefaultAsync(p => p.UserId == uid && p.Status == PlayerStatus.Active);

        if (gmPlayer == null || gmPlayer.Role != PlayerRole.Creator)
        {
            await Clients.Caller.SendAsync("Error", new { message = "Only the GM can send OOC whispers." });
            return;
        }

        var targetPlayer = await _context.Players
            .FirstOrDefaultAsync(p => p.Id == targetPlayerId && p.GameId == gmPlayer.GameId && p.Status == PlayerStatus.Active);

        if (targetPlayer == null)
        {
            await Clients.Caller.SendAsync("Error", new { message = "Target player not found or inactive." });
            return;
        }

        var whisper = await _whisperService.SendGMWhisperAsync(
            gmPlayer.GameId, Guid.Empty, gmPlayer.Id,
            new List<Guid> { targetPlayerId }, Adnd.Server.Models.WhisperType.OOCGMToPlayer, content);

        // Send to the target player
        var targetConnectionId = GetConnectionIdForPlayer(targetPlayer.Id);
        if (targetConnectionId != null)
        {
            await Clients.Client(targetConnectionId)
                .SendAsync("NewOOCWhisper", new
                {
                    whisper.Id,
                    FromPlayerId = whisper.FromPlayerId,
                    FromCharacter = gmPlayer.CharacterName,
                    FromRole = gmPlayer.Role,
                    Content = whisper.Content,
                    Type = whisper.Type,
                    Targets = whisper.Targets,
                    CreatedAt = whisper.CreatedAt,
                    IsReceived = true
                });
        }

        // Confirmation to GM
        await Clients.Caller.SendAsync("NewOOCWhisper", new
        {
            whisper.Id,
            FromPlayerId = whisper.FromPlayerId,
            FromCharacter = gmPlayer.CharacterName,
            FromRole = gmPlayer.Role,
            Content = whisper.Content,
            Type = whisper.Type,
            Targets = whisper.Targets,
            CreatedAt = whisper.CreatedAt,
            IsSent = true
        });

        _logger.LogInformation("OOC whisper from GM to {Target} in game {GameId}",
            targetPlayer.CharacterName, gmPlayer.GameId);
    }

    /// <summary>
    /// GM sends an in-game whisper to a player (e.g., divination result).
    /// </summary>
    public async Task SendInGameWhisperToPlayer(Guid targetPlayerId, string content)
    {
        var userId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var uid))
        {
            await Clients.Caller.SendAsync("Error", new { message = "Authentication required." });
            return;
        }

        var gmPlayer = await _context.Players
            .FirstOrDefaultAsync(p => p.UserId == uid && p.Status == PlayerStatus.Active);

        if (gmPlayer == null || gmPlayer.Role != PlayerRole.Creator)
        {
            await Clients.Caller.SendAsync("Error", new { message = "Only the GM can send in-game whispers." });
            return;
        }

        var targetPlayer = await _context.Players
            .FirstOrDefaultAsync(p => p.Id == targetPlayerId && p.GameId == gmPlayer.GameId && p.Status == PlayerStatus.Active);

        if (targetPlayer == null)
        {
            await Clients.Caller.SendAsync("Error", new { message = "Target player not found or inactive." });
            return;
        }

        var message = new Message
        {
            SessionId = Guid.Empty,
            PlayerId = gmPlayer.Id,
            Content = content,
            Type = Adnd.Server.Models.MessageType.InGameWhisper,
            IsOOC = false,
            WhisperFromId = gmPlayer.Id,
            WhisperToId = targetPlayerId,
            WhisperTarget = $"player:{targetPlayerId}",
            Metadata = default,
            CreatedAt = DateTime.UtcNow
        };

        _context.Messages.Add(message);
        await _context.SaveChangesAsync();

        // Generate embedding for in-game whisper (influences GM knowledge)
        await EmbedMessageAsync(gmPlayer.GameId, message.Id, message.Content);

        // Create whisper record
        var whisper = await _whisperService.SendGMWhisperAsync(
            gmPlayer.GameId, Guid.Empty, gmPlayer.Id,
            new List<Guid> { targetPlayerId }, Adnd.Server.Models.WhisperType.InGameGMToPlayer, content);

        // Send to the target player
        var targetConnectionId = GetConnectionIdForPlayer(targetPlayer.Id);
        if (targetConnectionId != null)
        {
            await Clients.Client(targetConnectionId)
                .SendAsync("NewWhisper", new
                {
                    whisper.Id,
                    FromPlayerId = whisper.FromPlayerId,
                    FromCharacter = gmPlayer.CharacterName,
                    FromRole = gmPlayer.Role,
                    Content = whisper.Content,
                    Type = whisper.Type,
                    Targets = whisper.Targets,
                    CreatedAt = whisper.CreatedAt,
                    IsReceived = true
                });
        }

        // Confirmation to GM
        await Clients.Caller.SendAsync("NewWhisper", new
        {
            whisper.Id,
            FromPlayerId = whisper.FromPlayerId,
            FromCharacter = gmPlayer.CharacterName,
            FromRole = gmPlayer.Role,
            Content = whisper.Content,
            Type = whisper.Type,
            Targets = whisper.Targets,
            CreatedAt = whisper.CreatedAt,
            IsSent = true
        });

        _logger.LogInformation("In-game whisper from GM to {Target} in game {GameId}",
            targetPlayer.CharacterName, gmPlayer.GameId);
    }

}
