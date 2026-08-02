using Adnd.Server.Data;
using Adnd.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Services;

public interface INPCRelevanceService
{
    /// <summary>
    /// The NPCs worth showing the GM for the situation at hand, most relevant first.
    /// </summary>
    Task<List<NPC>> GetRelevantAsync(Guid gameId, Guid? sessionId = null, int limit = DefaultLimit, CancellationToken ct = default);

    const int DefaultLimit = 8;
}

/// <summary>
/// A campaign accumulates NPCs indefinitely; a prompt cannot. Listing all of them buried the
/// two people actually standing in the room, and — worse — kept feeding the narrator characters
/// who were dead or long written out, which it then cheerfully brought back.
///
/// Relevance is deliberately cheap and explainable rather than semantic: whoever the last
/// stretch of table talk actually named, then the most recently touched living NPCs. NPCs have
/// no embedding column, and adding one to rank a list this short would cost an embedding call
/// per turn to answer a question recency already answers.
/// </summary>
public class NPCRelevanceService(AppDbContext db) : INPCRelevanceService
{
    private const int RecentMessageWindow = 20;

    /// <summary>A name fragment shorter than this matches half the English language.</summary>
    private const int MinimumFragmentLength = 4;

    public async Task<List<NPC>> GetRelevantAsync(
        Guid gameId, Guid? sessionId = null, int limit = INPCRelevanceService.DefaultLimit, CancellationToken ct = default)
    {
        if (limit <= 0) return [];

        var npcs = await db.NPCs
            .Where(n => n.GameId == gameId)
            .ToListAsync(ct);
        if (npcs.Count == 0) return [];

        var recentText = await GetRecentTableTextAsync(gameId, sessionId, ct);

        return npcs
            .Select(n => new { NPC = n, Mentioned = IsMentionedIn(n, recentText) })
            // A dead or departed NPC is not offered unprompted, but if the party is still
            // talking about them they belong in the prompt — otherwise the GM answers a
            // question about the corpse at their feet with no idea who it is.
            .Where(x => x.Mentioned || x.NPC.Status == NPCStatus.Active)
            .OrderByDescending(x => x.Mentioned)
            .ThenByDescending(x => x.NPC.LastSeenAt ?? DateTimeOffset.MinValue)
            .ThenBy(x => x.NPC.Name)
            .Take(limit)
            .Select(x => x.NPC)
            .ToList();
    }

    private async Task<string> GetRecentTableTextAsync(Guid gameId, Guid? sessionId, CancellationToken ct)
    {
        var resolvedSessionId = sessionId ?? await db.GameSessions
            .Where(s => s.GameId == gameId && s.Status == GameSessionStatus.Active)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => (Guid?)s.Id)
            .FirstOrDefaultAsync(ct);

        if (resolvedSessionId is null) return string.Empty;

        var contents = await db.Messages
            .Where(m => m.SessionId == resolvedSessionId && !m.IsOOC && m.WhisperToId == null)
            .OrderByDescending(m => m.CreatedAt)
            .Take(RecentMessageWindow)
            .Select(m => m.Content)
            .ToListAsync(ct);

        return string.Join('\n', contents).ToLowerInvariant();
    }

    /// <summary>
    /// Matches the full name, or its first word when that word is long enough to be
    /// distinctive — the table writes "Alaric", the roster says "Alaric the Grey".
    /// </summary>
    private static bool IsMentionedIn(NPC npc, string recentText)
    {
        if (recentText.Length == 0 || string.IsNullOrWhiteSpace(npc.Name)) return false;

        var name = npc.Name.Trim().ToLowerInvariant();
        if (recentText.Contains(name, StringComparison.Ordinal)) return true;

        var firstWord = name.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return firstWord is { Length: >= MinimumFragmentLength }
            && recentText.Contains(firstWord, StringComparison.Ordinal);
    }
}
