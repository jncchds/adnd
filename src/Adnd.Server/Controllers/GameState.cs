using Microsoft.AspNetCore.Mvc;
using Adnd.Server.Models;
using Adnd.Server.Hubs;
using Microsoft.EntityFrameworkCore;
using Adnd.Server.Services;
using System.Text.Json;

namespace Adnd.Server.Controllers;

public partial class AdminController
{
    // ==================== Game State (Comprehensive) ====================

    /// <summary>
    /// Get a comprehensive game state snapshot — everything the creator needs to see.
    /// </summary>
    [HttpGet("games/{gameId}/state")]
    public async Task<IActionResult> GetGameState(Guid gameId)
    {
        var userId = _userIdProvider.GetCurrentUserId();
        var game = await _context.Games
            .Include(g => g.LLMPreset)
            .Include(g => g.Players)
            .Include(g => g.Sessions)
            .Include(g => g.PlotThreads)
            .Include(g => g.NPCs)
            .FirstOrDefaultAsync(g => g.Id == gameId);

        if (game == null) return NotFound(new { error = "Game not found." });
        if (!await _authService.HasAccessAsync(_context, gameId, userId)) return Forbid();

        // Recent messages (last 30, including all types)
        var recentMessages = await _context.Messages
            .Where(m => m.Session!.GameId == gameId)
            .Include(m => m.Player)
            .OrderByDescending(m => m.CreatedAt)
            .Take(30)
            .ToListAsync();

        // Active combats
        var activeCombats = await _context.Combats
            .Where(c => c.GameId == gameId && c.Status == CombatStatus.Active)
            .ToListAsync();

        // Pending agent calls
        var pendingCalls = await _agentBus.GetPendingCallsAsync(AgentType.GM, 20);
        var runningCalls = await _context.AgentCalls
            .Where(a => a.GameId == gameId && a.Status == AgentCallStatus.Running)
            .OrderByDescending(a => a.StartedAt)
            .Take(10)
            .ToListAsync();

        // Pending tool calls
        var pendingTools = await _context.GMToolCalls
            .Where(t => t.GameId == gameId &&
                        (t.Status == ToolCallStatus.WaitingConfirmation || t.Status == ToolCallStatus.Pending))
            .OrderByDescending(t => t.CreatedAt)
            .Take(10)
            .ToListAsync();

        // Total LLM stats
        var llmStats = await _interactionLogger.GetGameLLMStatsAsync(gameId);

        // Provider breakdown
        var providerBreakdown = await _interactionLogger.GetGameProviderUsageAsync(gameId);

        // Connection status: count connected players via SignalR groups
        var connectedPlayerIds = GameHub.IsConnectedPlayers(gameId);

        // Plot review history
        var reviewHistory = await _plotWeaver.GetReviewHistoryAsync(gameId, 10);

        // Characters via players
        var playerIds = game.Players.Where(p => p.Status != PlayerStatus.Left).Select(p => p.Id).ToList();
        var characters = await _context.Characters
            .Where(c => playerIds.Contains(c.PlayerId))
            .ToListAsync();

        return Ok(new
        {
            Game = new
            {
                game.Id,
                game.Name,
                game.SystemId,
                game.SystemVersion,
                game.Status,
                game.GMStatus,
                game.LastGMAction,
                game.LastGMActionAt,
                game.CreatedAt,
                game.StartedAt,
                game.Language,
                game.PlotSeed,
                game.GameState,
                game.GameParameters,
                game.LLMPresetId,
                LLMPresetName = game.LLMPreset?.Name,
                LLMPresetProvider = game.LLMPreset?.ProviderType,
                LLMPresetModel = game.LLMPreset?.BaseModel,
            },
            Players = game.Players.Select(p => new
            {
                p.Id,
                p.CharacterName,
                p.Role,
                p.Status,
                p.JoinedAt,
                p.LeftAt,
                p.User?.DisplayName,
                p.User?.Email,
                IsConnected = connectedPlayerIds.Contains(p.Id),
            }).ToList(),
            PlayerStats = new
            {
                Total = game.Players.Count,
                Connected = connectedPlayerIds.Count,
                Left = game.Players.Count(p => p.Status == PlayerStatus.Left),
            },
            Sessions = game.Sessions.Select(s => new
            {
                s.Id,
                s.Title,
                s.Description,
                s.StartedAt,
                s.EndedAt,
                MessageCount = _context.Messages.Count(m => m.SessionId == s.Id),
            }).ToList(),
            SessionStats = new
            {
                Total = game.Sessions.Count,
                Active = game.Sessions.Count(s => s.EndedAt == null),
                Closed = game.Sessions.Count(s => s.EndedAt != null),
            },
            RecentMessages = recentMessages.Select(m => new
            {
                m.Id,
                m.SessionId,
                m.PlayerId,
                PlayerName = m.Player?.CharacterName ?? "System",
                m.Content,
                m.Type,
                m.IsOOC,
                m.CreatedAt,
            }).ToList(),
            ActiveCombats = activeCombats.Select(c => new
            {
                c.Id,
                c.Name,
                c.Status,
                c.CurrentRound,
                c.CurrentTurnIndex,
                c.StartedAt,
                c.EndedAt,
                Participants = c.Participants.Select(p => new
                {
                    p.Id,
                    p.DisplayName,
                    p.ParticipantType,
                    p.CurrentHP,
                    p.MaxHP,
                    p.AC,
                    p.Initiative,
                    p.ActionsRemaining,
                    p.BonusActionsRemaining,
                    p.ReactionsRemaining,
                    p.MovementsRemaining,
                    Conditions = p.Conditions,
                }).ToList(),
                Events = c.Events.OrderByDescending(e => e.CreatedAt).Take(20).Select(e => new
                {
                    e.Id,
                    e.Round,
                    e.TurnIndex,
                    e.Type,
                    e.ActorName,
                    e.TargetName,
                    e.Content,
                    e.CreatedAt,
                }).ToList(),
            }).ToList(),
            CombatStats = new
            {
                Active = activeCombats.Count,
                Total = await _context.Combats.CountAsync(c => c.GameId == gameId),
            },
            PlotThreads = game.PlotThreads
                .Where(t => t.Status == PlotThreadStatus.Active)
                .OrderByDescending(t => t.RelevanceScore)
                .ThenByDescending(t => t.Momentum)
                .Select(t => new
                {
                    t.Id,
                    t.Title,
                    t.Category,
                    t.Description,
                    t.Status,
                    t.Momentum,
                    t.RelevanceScore,
                    t.NextMilestone,
                    t.Foreshadowing,
                    t.AdaptationHistory,
                    t.MilestoneEvents,
                    t.CreatedAt,
                    t.UpdatedAt,
                }).ToList(),
            PlotStats = new
            {
                Active = game.PlotThreads.Count(t => t.Status == PlotThreadStatus.Active),
                Resolved = game.PlotThreads.Count(t => t.Status == PlotThreadStatus.Resolved),
                Abandoned = game.PlotThreads.Count(t => t.Status == PlotThreadStatus.Abandoned),
                RecentReviews = reviewHistory.Count,
            },
            AgentCalls = new
            {
                Pending = pendingCalls.Count,
                PendingList = pendingCalls.Select(c => new
                {
                    c.Id,
                    c.FromAgent,
                    c.ToAgent,
                    c.Action,
                    c.Input,
                    c.CreatedAt,
                }).ToList(),
                Running = runningCalls.Count,
                RunningList = runningCalls.Select(c => new
                {
                    c.Id,
                    c.FromAgent,
                    c.ToAgent,
                    c.Action,
                    c.Input,
                    c.StartedAt,
                    c.DurationMs,
                }).ToList(),
            },
            PendingToolCalls = pendingTools.Select(t => new
            {
                t.Id,
                t.ToolName,
                t.Status,
                t.OutputMessage,
                t.RequiresConfirmation,
                t.CreatedAt,
                t.Arguments,
            }).ToList(),
            LLMStats = new
            {
                TotalCalls = llmStats.TotalCalls,
                SuccessfulCalls = llmStats.SuccessfulCalls,
                FailedCalls = llmStats.FailedCalls,
                TotalTokens = llmStats.TotalTokens,
                TotalPromptTokens = llmStats.TotalPromptTokens,
                TotalCompletionTokens = llmStats.TotalCompletionTokens,
                AvgDurationMs = llmStats.AvgDurationMs,
            },
            LLMProviderBreakdown = providerBreakdown.Select(p => new
            {
                ProviderType = p.ProviderType,
                Model = p.Model,
                TotalCalls = p.TotalCalls,
                SuccessfulCalls = p.SuccessfulCalls,
                FailedCalls = p.FailedCalls,
                SuccessRate = p.SuccessRate,
                TotalTokens = p.TotalTokens,
                AvgDurationMs = p.AvgDurationMs,
            }).ToList(),
            NPCs = new
            {
                Count = game.NPCs.Count,
                List = game.NPCs.Select(n => new
                {
                    n.Id,
                    n.Name,
                    n.Description,
                    n.Attributes,
                    n.Skills,
                    n.CreatedAt,
                }).ToList(),
            },
            Characters = new
            {
                Count = characters.Count,
                List = characters.Select(c => new
                {
                    c.Id,
                    c.Name,
                    c.Class,
                    c.Level,
                    c.CurrentHP,
                    c.MaxHP,
                    c.Conditions,
                    c.Spells,
                    c.UpdatedAt,
                    PlayerName = c.Player?.CharacterName ?? "Unknown",
                }).ToList(),
            },
            GameState = game.GameState,
            PlotSeed = game.PlotSeed,
            GameParameters = game.GameParameters,
        });
    }

    [HttpPut("games/{gameId}/state")]
    public async Task<IActionResult> UpdateGameState(Guid gameId, [FromBody] UpdateGameStateRequest request)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null) return NotFound(new { error = "Game not found." });

        var userId = _userIdProvider.GetCurrentUserId();
        if (game.CreatorId != userId)
            return Forbid();

        if (request.GameState != null) game.GameState = request.GameState;
        if (request.PlotSeed != null) game.PlotSeed = request.PlotSeed;
        if (request.GameParameters != null) game.GameParameters = request.GameParameters;

        await _context.SaveChangesAsync();

        return Ok(new { gameId, game.GameState, game.PlotSeed, game.GameParameters });
    }

}
