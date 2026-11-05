using Adnd.Server.Services;

namespace Adnd.Server.Hubs;

/// <summary>Chat at the table. The rules live in <see cref="ITableChatService"/>, shared with the Telegram bot.</summary>
public partial class GameHub
{
    public Task SendMessage(Guid gameId, string content, bool isOOC = false)
        => Chat(() => chat.SayAsync(gameId, CurrentUserId, content, isOOC));

    public Task SendWhisper(Guid gameId, Guid targetPlayerId, string content, bool isOOC = false)
        => Chat(() => chat.WhisperAsync(gameId, CurrentUserId, targetPlayerId, content, isOOC));

    public async Task SendOOCMessage(Guid gameId, string content)
        => await SendMessage(gameId, content, isOOC: true);

    public async Task SendOOCWhisper(Guid gameId, Guid targetPlayerId, string content)
        => await SendWhisper(gameId, targetPlayerId, content, isOOC: true);

    /// <summary>A refusal from the chat service reaches the caller as a hub error with its message.</summary>
    private static async Task Chat(Func<Task> action)
    {
        try { await action(); }
        catch (TableChatException ex) { throw new HubForbiddenException(ex.Message); }
    }
}
