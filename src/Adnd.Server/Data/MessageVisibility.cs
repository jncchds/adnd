using System.Linq.Expressions;
using Adnd.Server.Models;

namespace Adnd.Server.Data;

/// <summary>
/// The single definition of "the GM saw this happen at the table".
///
/// Everything that feeds the narrator — recent-message context, the NPC relevance window,
/// the session recap, and the embedding column — must go through here. The rule has three
/// parts and they were previously spelled out inline, differently, in four places:
///
/// <list type="bullet">
/// <item>OOC chat is the players talking about the game, not in it. It must never reach
/// narration or the GM starts answering table logistics in character.</item>
/// <item>A player-to-player whisper is private between those characters. The GM is not in
/// that conversation and must not narrate as though it overheard it.</item>
/// <item>A GM-to-player whisper (and the private reply to a <c>TriggerSuggest</c> ask) was
/// told to one player. Feeding it back into the shared context leaks it to the whole table
/// through the next narration.</item>
/// </list>
///
/// The converse needs no rule of its own: a message a character says publicly carries no
/// whisper routing, so it is included — which is exactly what "unless they shared it
/// publicly" means. Deciding on the routing fields rather than <c>Type == "Whisper"</c>
/// matters, because a private GM-suggest reply is stored with <c>Type "GM"</c>.
/// </summary>
public static class MessageVisibility
{
    public static readonly Expression<Func<Message, bool>> IsVisibleToNarration =
        m => !m.IsOOC && m.WhisperFromId == null && m.WhisperToId == null;

    private static readonly Func<Message, bool> Compiled = IsVisibleToNarration.Compile();

    public static IQueryable<Message> VisibleToNarration(this IQueryable<Message> messages)
        => messages.Where(IsVisibleToNarration);

    /// <summary>In-memory form, for a message already loaded rather than queried.</summary>
    public static bool IsNarrationVisible(this Message message) => Compiled(message);
}
