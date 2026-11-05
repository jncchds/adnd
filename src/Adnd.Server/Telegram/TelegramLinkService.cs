using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Adnd.Server.Data;
using Adnd.Server.Models;
using Microsoft.EntityFrameworkCore;
using static Adnd.Server.Telegram.TelegramFormat;

namespace Adnd.Server.Telegram;

/// <summary>
/// Who is who on Telegram, and which chats play which game.
/// <list type="bullet">
/// <item><b>Accounts.</b> A user makes a one-time code in User Settings and opens <c>t.me/&lt;bot&gt;?start=acc_CODE</c>; the
/// bot links whoever sends it, in a private chat. A player acts in Telegram only through a linked account — a line
/// in a group is spoken by the linked user's character, a button press is theirs.</item>
/// <item><b>Groups.</b> The game's creator makes a one-time code in Game Settings; sent to a group
/// (<c>?startgroup=CODE</c>) or a forum topic (<c>/bind CODE</c>), that chat follows the game.</item>
/// <item><b>Private chats</b> follow one of their user's games, picked with /games — no code: the account is the key.</item>
/// </list>
/// Codes are stored as SHA-256 hashes and expire after 15 minutes.
/// </summary>
public sealed partial class TelegramLinkService(
    AppDbContext db,
    TelegramBotState bot,
    TelegramDispatcher dispatcher,
    TelegramApi api,
    ILogger<TelegramLinkService> logger)
{
    public const string AccountPrefix = "acc_";
    public static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(15);

    // Telegram user → ADnD user, for the check on every update (short-lived, so unlinking takes effect quickly).
    private static readonly ConcurrentDictionary<long, (Guid? UserId, DateTime Until)> LinkedCache = new();

    public sealed record Code(string Value, DateTimeOffset ExpiresAt);

    public async Task<Code> CreateCodeAsync(TelegramCodeKind kind, Guid userId, Guid? gameId)
    {
        await db.TelegramCodes.Where(c => c.ExpiresAt < DateTimeOffset.UtcNow.AddDays(-1)).ExecuteDeleteAsync();
        // Start parameters allow A-Z, a-z, 0-9, _ and - (up to 64 characters).
        var code = Convert.ToBase64String(RandomNumberGenerator.GetBytes(18)).Replace('+', '-').Replace('/', '_');
        var expires = DateTimeOffset.UtcNow + CodeLifetime;
        db.TelegramCodes.Add(new TelegramCode { CodeHash = HashCode(code), Kind = kind, UserId = userId, GameId = gameId, ExpiresAt = expires });
        await db.SaveChangesAsync();
        return new Code(code, expires);
    }

    public static string HashCode(string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code))).ToLowerInvariant();

    public static bool LooksLikeCode(string? text) => text is not null && CodePattern().IsMatch(text);

    [GeneratedRegex("^[A-Za-z0-9_-]{16,64}$")]
    private static partial Regex CodePattern();

    /// <summary>The unused, unexpired code of that kind, or why there is none (HTML).</summary>
    private async Task<(TelegramCode? Code, string? Error)> TakeAsync(string code, TelegramCodeKind kind)
    {
        var hash = LooksLikeCode(code) ? HashCode(code) : null;
        var found = hash is null ? null : await db.TelegramCodes.FirstOrDefaultAsync(c => c.CodeHash == hash && c.Kind == kind);
        var where = kind == TelegramCodeKind.Account ? "User Settings → Telegram" : "Game Settings → Telegram";
        if (found is null) return (null, $"❌ This code isn't valid. Make a new one in ADnD: {where}.");
        if (found.UsedAt is not null) return (null, $"❌ This code was already used. Make a new one in ADnD: {where}.");
        if (found.ExpiresAt < DateTimeOffset.UtcNow) return (null, $"❌ This code has expired. Make a new one in ADnD: {where}.");
        // Claimed in one statement, so two chats racing for one code cannot both have it.
        var claimed = await db.TelegramCodes.Where(c => c.Id == found.Id && c.UsedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.UsedAt, DateTimeOffset.UtcNow));
        return claimed == 1 ? (found, null) : (null, "❌ This code was already used.");
    }

    // ── Accounts ──────────────────────────────────────────────────────────────

    /// <summary>Links the sender to the code's user. Returns the reply (HTML).</summary>
    public async Task<string> LinkAsync(TgMessage message, string code)
    {
        if (message.Chat.Type != "private" || message.From is null)
            return "🔒 Link your account in a private chat with the bot, not here.";
        var from = message.From;
        var other = await db.Users.FirstOrDefaultAsync(u => u.TelegramUserId == from.Id);
        var (found, error) = await TakeAsync(code, TelegramCodeKind.Account);
        if (found is null) return error!;

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == found.UserId);
        if (user is null) return "❌ That ADnD account no longer exists.";
        if (other is not null && other.Id != user.Id)
            return $"❌ This Telegram account is already linked to <b>{H(other.DisplayName)}</b>. Send /unlink first.";

        // A user links one Telegram account: a new link replaces the old one.
        var previousChat = user.TelegramUserId;
        user.TelegramUserId = from.Id;
        user.TelegramUsername = Clip(from.Display, 100);
        user.TelegramLinkedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        LinkedCache.TryRemove(from.Id, out _);
        if (previousChat is long old && old != from.Id)
        {
            LinkedCache.TryRemove(old, out _);
            await EndPrivateBindingsAsync(user.Id, "linked to another Telegram account");
            dispatcher.Detach(old, null);
        }
        dispatcher.EnsurePrivate(user.Id, from.Id);
        logger.LogInformation("Telegram: user {UserId} linked Telegram account {TelegramId}.", user.Id, from.Id);
        return $"✅ Linked to <b>{H(user.DisplayName)}</b>.\n\n" +
               "Roll prompts and whispers from your games now come here, with their buttons. " +
               "/games to play one of your games in this chat; in a group that plays your game, just write to speak in character. /help lists everything.";
    }

    public async Task<bool> UnlinkAsync(User user)
    {
        if (user.TelegramUserId is not long telegramId) return false;
        await EndPrivateBindingsAsync(user.Id, "account unlinked");
        user.TelegramUserId = null;
        user.TelegramUsername = null;
        user.TelegramLinkedAt = null;
        await db.SaveChangesAsync();
        LinkedCache.TryRemove(telegramId, out _);
        dispatcher.Detach(telegramId, null);
        return true;
    }

    /// <summary>The ADnD user a Telegram account is linked to (cached for a minute).</summary>
    public async Task<User?> LinkedUserAsync(long telegramUserId)
    {
        if (LinkedCache.TryGetValue(telegramUserId, out var cached) && cached.Until > DateTime.UtcNow)
            return cached.UserId is { } id ? await db.Users.FirstOrDefaultAsync(u => u.Id == id) : null;
        var user = await db.Users.FirstOrDefaultAsync(u => u.TelegramUserId == telegramUserId);
        LinkedCache[telegramUserId] = (user?.Id, DateTime.UtcNow.AddMinutes(1));
        return user;
    }

    // ── Groups and topics ─────────────────────────────────────────────────────

    /// <summary>
    /// Makes the chat (and topic) <paramref name="message"/> was sent in follow the code's game. Returns null on
    /// success — the new chat's worker greets it — or the HTML reply explaining why not.
    /// </summary>
    public async Task<string?> BindGroupAsync(TgMessage message, string code)
    {
        if (message.Chat.Type is not ("group" or "supergroup"))
            return message.Chat.Type == "private"
                ? "ℹ️ This code is for a group: add the bot to the group (or use the group link from Game Settings), or send <code>/bind CODE</code> in a topic. Here, /games picks a game to play."
                : "❌ Channels can't play — use a group.";
        var chat = message.Chat;
        var thread = message.TopicId;
        if (await db.TelegramBindings.AsNoTracking().Include(b => b.Game)
                .FirstOrDefaultAsync(b => b.ChatId == chat.Id && b.ThreadId == thread && b.RevokedAt == null) is { } existing)
            return $"❌ This {(thread is null ? "group" : "topic")} already plays “{H(existing.Game.Name)}”. Send /unbind first" +
                   (thread is null ? ", or use one of its topics." : ", or use another topic.");

        var (found, error) = await TakeAsync(code, TelegramCodeKind.Bind);
        if (found is null) return error!;
        var game = await db.Games.FirstOrDefaultAsync(g => g.Id == found.GameId);
        if (game is null) return "❌ The game no longer exists.";

        var binding = new TelegramBinding
        {
            GameId = game.Id,
            ChatId = chat.Id,
            ThreadId = thread,
            ChatType = chat.Type,
            ChatTitle = Clip(chat.Display + (thread is not null ? " · topic" : ""), 300),
            CreatedByUserId = found.UserId,
        };
        db.TelegramBindings.Add(binding);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return "❌ This chat was connected to a game a moment ago. Send /unbind first.";
        }

        await RecordExistingAsync(binding);
        dispatcher.AttachGroup(binding);
        logger.LogInformation("Telegram: game {GameId} bound to chat {ChatId} topic {ThreadId}.", game.Id, chat.Id, thread);
        return null;
    }

    /// <summary>
    /// What the game already has is recorded as sent (message 0), so only later lines reach the chat — no replay of
    /// the whole campaign. Only the last 500 public messages: older ones never change.
    /// </summary>
    private async Task RecordExistingAsync(TelegramBinding binding)
    {
        var thread = binding.ThreadId ?? 0;
        var ids = await db.Messages.AsNoTracking()
            .Where(m => m.Session.GameId == binding.GameId)
            .OrderByDescending(m => m.CreatedAt).Take(500).Select(m => m.Id).ToListAsync();
        // The chat may have followed the game before, or (a private chat) had its prompts already.
        var linked = (await db.TelegramMessageLinks.AsNoTracking()
            .Where(l => l.ChatId == binding.ChatId && l.ThreadId == thread && l.Kind == TelegramLinkKind.Message && ids.Contains(l.RefId))
            .Select(l => l.RefId).ToListAsync()).ToHashSet();
        foreach (var id in ids.Where(i => !linked.Contains(i)))
            db.TelegramMessageLinks.Add(new TelegramMessageLink
            {
                ChatId = binding.ChatId, ThreadId = thread, Kind = TelegramLinkKind.Message, RefId = id,
                TelegramMessageId = 0, ContentHash = "existing",
            });
        await db.SaveChangesAsync();
    }

    // ── Private chats ─────────────────────────────────────────────────────────

    /// <summary>
    /// The user's private chat follows <paramref name="gameId"/> from now on (one game at a time). Null on success —
    /// the chat's worker says so — or the HTML reply explaining why not.
    /// </summary>
    public async Task<string?> FollowAsync(User user, long chatId, Guid gameId)
    {
        var player = await db.Players.Include(p => p.Game).FirstOrDefaultAsync(p => p.GameId == gameId && p.UserId == user.Id);
        if (player is null) return "❌ You are not in that game.";
        var current = await db.TelegramBindings
            .FirstOrDefaultAsync(b => b.ChatId == chatId && b.ThreadId == null && b.RevokedAt == null);
        if (current?.GameId == gameId) return $"ℹ️ This chat already plays <b>{H(player.Game.Name)}</b>.";
        if (current is not null)
        {
            current.RevokedAt = DateTimeOffset.UtcNow;
            current.RevokedReason = "switched to another game";
            await db.SaveChangesAsync();
        }

        var binding = new TelegramBinding
        {
            GameId = gameId, ChatId = chatId, ChatType = "private", UserId = user.Id, CreatedByUserId = user.Id,
            ChatTitle = Clip($"Private chat with {user.TelegramUsername ?? user.DisplayName}", 300),
        };
        db.TelegramBindings.Add(binding);
        await db.SaveChangesAsync();
        await RecordExistingAsync(binding);
        dispatcher.EnsurePrivate(user.Id, chatId);
        dispatcher.Enqueue(chatId, null, async w =>
        {
            await w.SendTextAsync($"▶ This chat now plays <b>{H(player.Game.Name)}</b>. Write here to speak in character; " +
                                  "/ooc for table talk, /ask to ask the GM privately, /w to whisper, /stop to stop following it.");
            using var scope = dispatcher.Scopes.CreateScope();
            await w.SyncCombatCardAsync(scope.ServiceProvider, fresh: false);
        });
        return null;
    }

    /// <summary>The private chat stops following its game; prompts and whispers still arrive.</summary>
    public async Task<bool> UnfollowAsync(User user, long chatId)
    {
        var current = await db.TelegramBindings
            .FirstOrDefaultAsync(b => b.ChatId == chatId && b.ThreadId == null && b.RevokedAt == null && b.UserId == user.Id);
        if (current is null) return false;
        current.RevokedAt = DateTimeOffset.UtcNow;
        current.RevokedReason = "stopped following";
        await db.SaveChangesAsync();
        dispatcher.EnsurePrivate(user.Id, chatId);
        return true;
    }

    private async Task EndPrivateBindingsAsync(Guid userId, string reason)
    {
        var bindings = await db.TelegramBindings.Where(b => b.UserId == userId && b.RevokedAt == null).ToListAsync();
        foreach (var b in bindings)
        {
            b.RevokedAt = DateTimeOffset.UtcNow;
            b.RevokedReason = reason;
        }
        await db.SaveChangesAsync();
    }

    // ── Ending ────────────────────────────────────────────────────────────────

    /// <summary>Ends a group binding. <paramref name="tellChat"/> posts a last notice there (best effort).</summary>
    public async Task RevokeAsync(TelegramBinding binding, string reason, bool tellChat)
    {
        if (!binding.IsActive) return;
        binding.RevokedAt = DateTimeOffset.UtcNow;
        binding.RevokedReason = Clip(reason, 300);
        await db.SaveChangesAsync();
        if (binding.IsPrivate && binding.UserId is { } userId) dispatcher.EnsurePrivate(userId, binding.ChatId);
        else dispatcher.Detach(binding.ChatId, binding.ThreadId);

        if (!tellChat) return;
        var gameName = await db.Games.IgnoreQueryFilters().Where(g => g.Id == binding.GameId).Select(g => g.Name).FirstOrDefaultAsync();
        try
        {
            await api.SendMessageAsync(binding.ChatId, binding.ThreadId,
                $"🔌 This chat no longer plays <b>{H(gameName ?? "the game")}</b>: {H(reason)}");
        }
        catch (Exception ex) when (ex is TelegramApiException or HttpRequestException or TaskCanceledException)
        {
            logger.LogInformation("Telegram: could not post the unbind notice to {ChatId}: {Message}", binding.ChatId, ex.Message);
        }
    }

    public string? GroupLink(string code) => bot.Username is null ? null : $"https://t.me/{bot.Username}?startgroup={code}";
    public string? AccountLink(string code) => bot.Username is null ? null : $"https://t.me/{bot.Username}?start={AccountPrefix}{code}";
}
