using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Adnd.Server.Events;
using MediatR;

namespace Adnd.Server.Hubs;

public partial class GameHub
{
    // ==================== Unified Chat Message ====================

    /// <summary>
    /// Send a chat message — unified entry point for all message types.
    /// messageType: "inGame" (public in-game), "ooc" (public OOC),
    ///              "inGameWhisper" (to GM), "oocWhisper" (OOC to GM),
    ///              "gmToPlayer" (GM → player whisper).
    /// </summary>
    public async Task SendMessage(string messageType, string content, Guid? targetPlayerId = null)
    {
        var userId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var uid))
        {
            await Clients.Caller.SendAsync("Error", new { message = "Authentication required." });
            return;
        }

        // Find player without filtering by status (connection tracked via SignalR groups)
        var player = await _context.Players
            .FirstOrDefaultAsync(p => p.UserId == uid);

        if (player == null || player.Game == null)
        {
            await Clients.Caller.SendAsync("Error", new { message = "You are not a player in this game." });
            return;
        }

        var gameId = player.GameId;

        // Resolve the game's current session (auto-created, single per game)
        var game = await _context.Games
            .Include(g => g.CurrentSession)
            .FirstOrDefaultAsync(g => g.Id == gameId);

        if (game == null || game.CurrentSession == null)
        {
            await Clients.Caller.SendAsync("Error", new { message = "Game session not found." });
            return;
        }

        var sessionId = game.CurrentSession.Id;

        // Determine message type and metadata
        var (messageTypeValue, whisperType, whisperTargets) = ResolveMessageDetails(messageType, player, targetPlayerId);

        // Create the message record
        var message = new Message
        {
            SessionId = sessionId,
            PlayerId = player.Id,
            Content = content,
            Type = messageTypeValue,
            IsOOC = messageType == "ooc" || messageType == "oocWhisper",
            WhisperFromId = messageType.Contains("Whisper") || messageType == "gmToPlayer" ? player.Id : (Guid?)null,
            WhisperToId = messageType == "gmToPlayer" ? targetPlayerId : (Guid?)null,
            WhisperTarget = whisperTargets,
            Metadata = default,
            CreatedAt = DateTime.UtcNow
        };

        _context.Messages.Add(message);
        await _context.SaveChangesAsync();

        // Generate embedding for narrative-influencing messages
        if (!message.IsOOC)
        {
            await EmbedMessageAsync(gameId, message.Id, message.Content);
        }

        // Publish appropriate event
        await PublishChatEvent(gameId, sessionId, player, content, whisperType, whisperTargets);

        // Broadcast to caller and/or group
        await BroadcastMessage(gameId, message, player, messageType);

        _logger.LogInformation("Message sent: {MessageType} by {CharacterName} in game {GameId}",
            messageType, player.CharacterName, gameId);
    }

    /// <summary>
    /// Resolve message type enum, whisper type, and target string from the client message type.
    /// </summary>
    private static (Models.MessageType messageType, Models.WhisperType whisperType, string? targets) ResolveMessageDetails(
        string messageType, Player player, Guid? targetPlayerId)
    {
        switch (messageType)
        {
            case "inGame":
                return (Models.MessageType.InGamePublic, 0, null);

            case "ooc":
                return (Models.MessageType.OOCPublic, 0, null);

            case "inGameWhisper":
                return (Models.MessageType.InGameWhisper, Models.WhisperType.InGamePlayerToGM, "gm");

            case "oocWhisper":
                return (Models.MessageType.OOCWhisper, Models.WhisperType.OOCPlayerToGM, "gm");

            case "gmToPlayer":
                if (player.Role != PlayerRole.Creator)
                    return (Models.MessageType.System, 0, null); // will be rejected later
                return (Models.MessageType.InGameWhisper, Models.WhisperType.InGameGMToPlayer, $"player:{targetPlayerId}");

            default:
                return (Models.MessageType.InGamePublic, 0, null);
        }
    }

    private async Task PublishChatEvent(Guid gameId, Guid sessionId, Player player, string content, Models.WhisperType whisperType, string? targets)
    {
        if (string.IsNullOrEmpty(targets) || targets == "all")
        {
            // Public message — publish as MessageSent
            var msgType = player.Game!.GMStatus == GMStatus.Paused ? Events.MessageType.OOCPublic :
                (whisperType == Models.WhisperType.InGamePlayerToGM || whisperType == Models.WhisperType.OOCPlayerToGM ?
                    Events.MessageType.OOCPublic : Events.MessageType.InGamePublic);
            await _mediator.Publish(new MessageSent(gameId, sessionId, player.Id, content, msgType, null, player.Game.GMStatus == GMStatus.Paused));
        }
        else if (targets == "gm")
        {
            // Whisper — publish as WhisperSent
            await _mediator.Publish(new WhisperSent(gameId, player.Id, "gm", content, (Events.WhisperType)whisperType));
        }
        else if (targets?.StartsWith("player:", StringComparison.Ordinal) == true)
        {
            // GM → player whisper — publish OOC whisper
            await _mediator.Publish(new OOCWhisperSent(gameId, player.Id, targets, content));
        }
    }

    private async Task BroadcastMessage(Guid gameId, Message message, Player player, string messageType)
    {
        var isWhisper = messageType.Contains("Whisper") || messageType == "gmToPlayer";

        if (isWhisper)
        {
            // Whisper: send via WhisperService for targeted delivery
            var whisper = await _whisperService.SendWhisperAsync(
                gameId, message.SessionId, player.Id,
                message.WhisperTarget ?? "all",
                messageType == "gmToPlayer" ? Models.WhisperType.InGameGMToPlayer :
                (messageType == "oocWhisper" ? Models.WhisperType.OOCPlayerToGM : Models.WhisperType.InGamePlayerToGM),
                message.Content);

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

            // Send to GM if player whisper
            if (messageType == "inGameWhisper" || messageType == "oocWhisper")
            {
                var gmPlayer = await _context.Players
                    .FirstOrDefaultAsync(p => p.GameId == gameId && p.Role == PlayerRole.Creator && p.Status == PlayerStatus.Active);

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
            }
        }
        else
        {
            // Public message — broadcast to all in game group
            await Clients.Group(gameId.ToString()).SendAsync("NewMessage", new
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
    }
}
