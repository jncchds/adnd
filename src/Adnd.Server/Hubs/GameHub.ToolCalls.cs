using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Adnd.Server.Events;

namespace Adnd.Server.Hubs;

public partial class GameHub
{
    // ==================== GM Tool Calls ====================

    /// <summary>
    /// Confirm a tool call that requires user input (e.g., ask a player to roll).
    /// The GM approves, and the tool proceeds.
    /// </summary>
    public async Task<ToolCallConfirmationResponse> ConfirmToolCall(Guid toolCallId, bool approved)
    {
        var userId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var uid))
            throw new UnauthorizedAccessException();

        var gmPlayer = await _context.Players
            .FirstOrDefaultAsync(p => p.UserId == uid && p.Status == PlayerStatus.Active);

        if (gmPlayer == null || gmPlayer.Role != PlayerRole.Creator)
            throw new ForbiddenException("Only the GM can confirm tool calls.");

        var toolCall = await _context.GMToolCalls.FindAsync(toolCallId);
        if (toolCall == null)
            throw new KeyNotFoundException($"Tool call {toolCallId} not found.");

        if (toolCall.Status != ToolCallStatus.WaitingConfirmation)
            throw new InvalidOperationException($"Tool call is not waiting confirmation (status: {toolCall.Status}).");

        if (approved)
        {
            toolCall.Status = ToolCallStatus.Confirmed;
            toolCall.ConfirmedBy = gmPlayer.Id;
            toolCall.ConfirmedAt = DateTime.UtcNow;

            // Re-execute the tool
            var result = await _toolRegistry.ExecuteToolAsync(toolCall.GameId, toolCall.SessionId ?? Guid.Empty,
                toolCall.ToolName, toolCall.Arguments!);

            toolCall.Status = result.Success ? ToolCallStatus.Completed : ToolCallStatus.Failed;
            toolCall.Result = result.Output;
            toolCall.OutputMessage = result.OutputMessage;
            toolCall.Error = result.Error;
            toolCall.CompletedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Notify all players in the game
            await Clients.Group(toolCall.GameId.ToString()).SendAsync("ToolCallConfirmed",
                new { toolCall.Id, toolCall.ToolName, toolCall.OutputMessage, toolCall.Result, approved = true });
        }
        else
        {
            toolCall.Status = ToolCallStatus.Cancelled;
            toolCall.Error = "Cancelled by GM";
            toolCall.CompletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            await Clients.Group(toolCall.GameId.ToString()).SendAsync("ToolCallConfirmed",
                new { toolCall.Id, toolCall.ToolName, approved = false });
        }

        return new ToolCallConfirmationResponse
        {
            Id = toolCall.Id,
            ToolName = toolCall.ToolName,
            Approved = approved,
            OutputMessage = toolCall.OutputMessage,
            Status = toolCall.Status
        };
    }

    /// <summary>
    /// Player confirms they will roll (for player-initiated rolls).
    /// Returns the roll formula so the player can roll in their UI.
    /// </summary>
    public async Task<PlayerRollConfirmationResponse> ConfirmPlayerRoll(Guid toolCallId)
    {
        var userId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var uid))
            throw new UnauthorizedAccessException();

        var player = await _context.Players
            .FirstOrDefaultAsync(p => p.UserId == uid && p.Status == PlayerStatus.Active);

        if (player == null)
            throw new InvalidOperationException("Player not active in any game.");

        var toolCall = await _context.GMToolCalls.FindAsync(toolCallId);
        if (toolCall == null)
            throw new KeyNotFoundException($"Tool call {toolCallId} not found.");

        if (toolCall.Status != ToolCallStatus.WaitingConfirmation)
            throw new InvalidOperationException($"Tool call is not waiting confirmation.");

        // Check if this player is in the target list
        var args = JsonSerializer.Deserialize<Dictionary<string, object>>(toolCall.Arguments ?? "{}") ?? new();
        var targetPlayerIds = new List<Guid>();
        if (args.TryGetValue("playerIds", out var pids) && pids is System.Collections.IEnumerable pidsList)
        {
            foreach (var pid in pidsList)
            {
                if (pid is string ps && Guid.TryParse(ps, out var guid))
                    targetPlayerIds.Add(guid);
            }
        }
        else if (args.TryGetValue("playerId", out var pidObj) && pidObj is string ps2 && Guid.TryParse(ps2, out var guid2))
        {
            targetPlayerIds.Add(guid2);
        }

        // If no specific targets, anyone can confirm
        if (targetPlayerIds.Count > 0 && !targetPlayerIds.Contains(player.Id))
            throw new ForbiddenException("This roll was not assigned to you.");

        // Mark as confirmed
        toolCall.Status = ToolCallStatus.Confirmed;
        toolCall.ConfirmedBy = player.Id;
        toolCall.ConfirmedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Send the roll details to the player
        var result = new PlayerRollConfirmationResponse
        {
            ToolCallId = toolCall.Id,
            Approved = true,
            Skill = args.GetValueOrDefault("skill")?.ToString() ?? "",
            Formula = args.GetValueOrDefault("formula")?.ToString() ?? "1d20",
            DC = args.GetValueOrDefault("dc") is int dc ? dc : 15,
            Context = args.GetValueOrDefault("context")?.ToString() ?? "",
            Optional = args.GetValueOrDefault("optional") is bool o && o
        };

        // Notify the player with roll details
        await Clients.Caller.SendAsync("PlayerRollRequested", new
        {
            toolCall.Id,
            result.Skill,
            result.Formula,
            result.DC,
            result.Context,
            result.Optional,
            result.Approved
        });

        // Notify others that this player confirmed
        await Clients.Group(toolCall.GameId.ToString()).SendAsync("PlayerRollConfirmed", new
        {
            toolCall.Id,
            ToolName = toolCall.ToolName,
            PlayerId = player.Id,
            PlayerName = player.CharacterName,
            Skill = result.Skill,
            Formula = result.Formula,
            DC = result.DC
        });

        return new PlayerRollConfirmationResponse
        {
            ToolCallId = toolCall.Id,
            Approved = true,
            Skill = result.Skill,
            Formula = result.Formula,
            DC = result.DC,
            Context = result.Context,
            Optional = result.Optional
        };
    }

    /// <summary>
    /// Player declines a roll request.
    /// </summary>
    public async Task DeclinePlayerRoll(Guid toolCallId)
    {
        var userId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var uid))
            throw new UnauthorizedAccessException();

        var player = await _context.Players
            .FirstOrDefaultAsync(p => p.UserId == uid && p.Status == PlayerStatus.Active);

        if (player == null)
            throw new InvalidOperationException("Player not active in any game.");

        var toolCall = await _context.GMToolCalls.FindAsync(toolCallId);
        if (toolCall == null)
            throw new KeyNotFoundException($"Tool call {toolCallId} not found.");

        if (toolCall.Status != ToolCallStatus.WaitingConfirmation)
            throw new InvalidOperationException($"Tool call is not waiting confirmation.");

        toolCall.Status = ToolCallStatus.Cancelled;
        toolCall.Error = "Declined by player";
        toolCall.CompletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Notify the game
        await Clients.Group(toolCall.GameId.ToString()).SendAsync("PlayerRollDeclined", new
        {
            toolCall.Id,
            PlayerId = player.Id,
            PlayerName = player.CharacterName,
            Skill = JsonSerializer.Deserialize<Dictionary<string, object>>(toolCall.Arguments ?? "{}")?.GetValueOrDefault("skill")?.ToString() ?? ""
        });

        // Notify the GM
        var gmPlayer = await _context.Players
            .FirstOrDefaultAsync(p => p.GameId == toolCall.GameId && p.Role == PlayerRole.Creator && p.Status == PlayerStatus.Active);

        if (gmPlayer != null)
        {
            var gmConnectionId = GetConnectionIdForPlayer(gmPlayer.Id);
            if (gmConnectionId != null)
            {
                await Clients.Client(gmConnectionId).SendAsync("PlayerRollDeclined", new
                {
                    toolCall.Id,
                    PlayerId = player.Id,
                    PlayerName = player.CharacterName,
                    Skill = JsonSerializer.Deserialize<Dictionary<string, object>>(toolCall.Arguments ?? "{}")?.GetValueOrDefault("skill")?.ToString() ?? ""
                });
            }
        }
    }

    /// <summary>
    /// Get pending tool calls for a game.
    /// </summary>
    public async Task<List<ToolCallInfo>> GetPendingToolCalls(Guid gameId)
    {
        var userId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var uid))
            throw new UnauthorizedAccessException();

        var game = await _context.Games.FindAsync(gameId);
        if (game == null)
            throw new KeyNotFoundException("Game not found.");

        var hasAccess = game.CreatorId == uid || game.Players.Any(p => p.UserId == uid);
        if (!hasAccess)
            throw new ForbiddenException("You don't have access to this game.");

        var pendingCalls = await _context.GMToolCalls
            .Where(t => t.GameId == gameId &&
                        (t.Status == ToolCallStatus.WaitingConfirmation || t.Status == ToolCallStatus.Pending))
            .OrderByDescending(t => t.CreatedAt)
            .Take(20)
            .ToListAsync();

        return pendingCalls.Select(t => new ToolCallInfo
        {
            Id = t.Id,
            ToolName = t.ToolName,
            Status = t.Status,
            Arguments = t.Arguments,
            OutputMessage = t.OutputMessage,
            CreatedAt = t.CreatedAt,
            RequiresConfirmation = t.RequiresConfirmation
        }).ToList();
    }

    /// <summary>
    /// Broadcast a tool call notification to all players in the game.
    /// Called when the GM agent requests a tool that requires confirmation.
    /// </summary>
    public async Task BroadcastToolCallNotification(Guid gameId, string toolCallId, string toolName, string outputMessage)
    {
        await Clients.Group(gameId.ToString()).SendAsync("ToolCallNotification", new
        {
            toolCallId,
            toolName,
            outputMessage,
            requiresConfirmation = true,
            timestamp = DateTime.UtcNow
        });
    }

}
