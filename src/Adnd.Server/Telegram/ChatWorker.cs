using System.Collections.Immutable;
using System.Threading.Channels;
using Adnd.Server.Data;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Microsoft.EntityFrameworkCore;
using static Adnd.Server.Telegram.TelegramFormat;

namespace Adnd.Server.Telegram;

/// <summary>
/// Everything the bot sends to one chat, one thing at a time and paced to Telegram's limits (about one message a
/// second in a private chat, twenty a minute in a group).
/// <list type="bullet">
/// <item>A <b>group or topic</b> follows one game (<see cref="Binding"/>) and is the table: it gets what the whole
/// table sees, the combat card, "typing…" while the GM works and the GM's failures.</item>
/// <item>A <b>private chat</b> belongs to one linked user (<see cref="UserId"/>). When it follows a game it gets that
/// game as the user sees it — their whispers, roll prompts and secret rolls too; from every other game they play it
/// gets only what is for them alone (prompts, whispers), under the game's name, so a roll asked of a player who
/// plays in a group reaches them with its buttons.</item>
/// </list>
/// Every message is read back from the database when it is sent or changed, so the rule for who may see it is the
/// chat history's, and a roll prompt answered on the web is redrawn here.
/// </summary>
public sealed class ChatWorker
{
    private static readonly TimeSpan MemberRefresh = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan TypingEvery = TimeSpan.FromSeconds(4.5);
    private static readonly TimeSpan TypingCap = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan CombatDebounce = TimeSpan.FromSeconds(1.5);
    private static readonly TimeSpan OwnLineWindow = TimeSpan.FromMinutes(2);

    private readonly TelegramDispatcher _d;
    private readonly Channel<object> _inbox = Channel.CreateUnbounded<object>(new UnboundedChannelOptions { SingleReader = true });
    private readonly CancellationTokenSource _cts = new();
    private readonly bool _isNew, _reloadFirst;

    private volatile TelegramBinding? _binding;
    private volatile ImmutableHashSet<Guid> _memberGames = [];
    private DateTime _memberGamesAt = DateTime.MinValue;

    private DateTime _lastSend = DateTime.MinValue, _lastTyping = DateTime.MinValue;
    private DateTime? _gmBusySince, _combatDueAt;

    /// <summary>Lines a player just typed in this chat: when the game posts them, they are linked, not echoed.</summary>
    private readonly List<(Guid PlayerId, string Content, int TelegramMessageId, DateTime At)> _ownLines = [];

    /// <summary>When the bot last told someone here that plain messages need a linked account (per Telegram user).</summary>
    public Dictionary<long, DateTime> LastHint { get; } = [];

    public ChatWorker(TelegramDispatcher dispatcher, long chatId, int? threadId, Guid? userId, TelegramBinding? binding,
        bool isNew, bool reloadFirst = false)
    {
        _d = dispatcher;
        ChatId = chatId;
        ThreadId = threadId;
        UserId = userId;
        _binding = binding;
        _isNew = isNew;
        _reloadFirst = reloadFirst;
    }

    public long ChatId { get; private set; }
    public int? ThreadId { get; }
    private int ThreadKey => ThreadId ?? 0;
    /// <summary>The linked user a private chat belongs to; null for a group.</summary>
    public Guid? UserId { get; }
    public bool IsPrivate => UserId is not null;
    /// <summary>The group's binding, or the game a private chat follows (null: it follows none).</summary>
    public TelegramBinding? Binding => _binding;
    public Guid? FollowedGameId => _binding?.GameId;

    /// <summary>Called from the dispatcher's thread: whether this chat has anything to do with <paramref name="gameId"/>.</summary>
    public bool Wants(Guid gameId) => FollowedGameId == gameId || (IsPrivate && _memberGames.Contains(gameId));

    public void Start(CancellationToken stopping)
    {
        stopping.Register(() => _cts.Cancel());
        _ = Task.Run(RunAsync);
    }

    public void Stop() => _cts.Cancel();
    public void Post(TgGameEvent ev) => _inbox.Writer.TryWrite(ev);
    public void Enqueue(Func<ChatWorker, Task> job) => _inbox.Writer.TryWrite(job);

    /// <summary>
    /// A player is about to say <paramref name="content"/> from this chat (Telegram message
    /// <paramref name="telegramMessageId"/>): when the game posts it, link it to their message instead of echoing it.
    /// Queued before the line is sent, so it is in place before the game's event arrives.
    /// </summary>
    public void ExpectOwnLine(Guid playerId, string content, int telegramMessageId) => Enqueue(_ =>
    {
        _ownLines.RemoveAll(l => DateTime.UtcNow - l.At > OwnLineWindow);
        _ownLines.Add((playerId, content, telegramMessageId, DateTime.UtcNow));
        return Task.CompletedTask;
    });

    private async Task RunAsync()
    {
        var ct = _cts.Token;
        try
        {
            if (_reloadFirst) await ReloadAsync();
            else await WithScopeAsync(RefreshMemberGamesAsync);
            if (_isNew) await WithScopeAsync(WelcomeAsync);
            while (!ct.IsCancellationRequested)
            {
                using (var wait = CancellationTokenSource.CreateLinkedTokenSource(ct))
                {
                    wait.CancelAfter(TimeSpan.FromSeconds(1));
                    try { await _inbox.Reader.WaitToReadAsync(wait.Token); }
                    catch (OperationCanceledException) when (!ct.IsCancellationRequested) { }
                }
                while (_inbox.Reader.TryRead(out var item))
                {
                    if (item is TgGameEvent ev) await GuardAsync(() => OnEventAsync(ev));
                    else if (item is Func<ChatWorker, Task> job) await GuardAsync(() => job(this));
                }
                await GuardAsync(DoDueWorkAsync);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (ChatGoneException) { }
        finally
        {
            _d.OnWorkerEnded(this);
        }
    }

    /// <summary>Runs one piece of work; a failure is logged and never stops the loop (only a gone chat does).</summary>
    private async Task GuardAsync(Func<Task> work)
    {
        try { await work(); }
        catch (ChatGoneException) { throw; }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            _d.Logger.LogWarning(ex, "Telegram: chat {ChatId} — {Message}", ChatId, ex.Message);
        }
    }

    private async Task WithScopeAsync(Func<IServiceProvider, Task> work)
    {
        using var scope = _d.Scopes.CreateScope();
        await work(scope.ServiceProvider);
    }

    // ── Events ────────────────────────────────────────────────────────────────

    private async Task OnEventAsync(TgGameEvent ev)
    {
        var followed = FollowedGameId == ev.GameId;
        switch (ev)
        {
            case TgMessageEvent m:
                await WithScopeAsync(sp => SyncMessageAsync(sp, m.GameId, m.MessageId, personalOnly: !followed));
                break;
            case TgCombatEvent when followed:
                _combatDueAt ??= DateTime.UtcNow + CombatDebounce;
                break;
            case TgActivityEvent a when followed:
                if (a.Step is nameof(SagaStep.Completed) or nameof(SagaStep.Failed)) _gmBusySince = null;
                else _gmBusySince ??= DateTime.UtcNow;
                break;
            case TgErrorEvent e when followed:
                await SendTextAsync($"⚠️ The GM could not finish its turn: {H(Clip(e.Message, 500))}", silent: true);
                break;
        }
    }

    private async Task DoDueWorkAsync()
    {
        var now = DateTime.UtcNow;
        if (IsPrivate && now - _memberGamesAt > MemberRefresh)
            await WithScopeAsync(RefreshMemberGamesAsync);

        if (_combatDueAt is { } due && due <= now)
        {
            _combatDueAt = null;
            await WithScopeAsync(sp => SyncCombatCardAsync(sp, fresh: false));
        }

        if (_gmBusySince is { } since)
        {
            if (now - since > TypingCap) _gmBusySince = null;
            else if (now - _lastTyping >= TypingEvery)
            {
                _lastTyping = now;
                try { await _d.Api.SendChatActionAsync(ChatId, ThreadId, ct: _cts.Token); }
                catch (TelegramApiException ex) { _d.Logger.LogDebug("Telegram: typing in {ChatId} — {Message}", ChatId, ex.Message); }
            }
        }
    }

    // ── Messages ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Sends, redraws or deletes the chat's copy of one message, as it now stands. <paramref name="personalOnly"/>:
    /// the chat does not follow the game, so only what is for its user alone is shown.
    /// </summary>
    private async Task SyncMessageAsync(IServiceProvider sp, Guid gameId, Guid messageId, bool personalOnly)
    {
        var db = sp.GetRequiredService<AppDbContext>();
        var message = await db.Messages.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == messageId && m.Session.GameId == gameId, _cts.Token);

        Player? viewer = null;
        if (UserId is { } uid)
        {
            viewer = await db.Players.AsNoTracking().FirstOrDefaultAsync(p => p.GameId == gameId && p.UserId == uid, _cts.Token);
            if (viewer is null) return;
        }

        var visible = message is not null && Visible(message, viewer) && (!personalOnly || !Visible(message, null));
        var link = await db.TelegramMessageLinks.FirstOrDefaultAsync(l =>
            l.ChatId == ChatId && l.ThreadId == ThreadKey && l.Kind == TelegramLinkKind.Message && l.RefId == messageId, _cts.Token);

        // A line typed here: it is already in the chat, as the player wrote it.
        if (link is null && visible && message!.PlayerId is { } speaker
            && _ownLines.FindIndex(l => l.PlayerId == speaker && l.Content == message.Content) is var own and >= 0)
        {
            db.TelegramMessageLinks.Add(new TelegramMessageLink
            {
                ChatId = ChatId, ThreadId = ThreadKey, Kind = TelegramLinkKind.Message, RefId = messageId,
                TelegramMessageId = _ownLines[own].TelegramMessageId, ContentHash = TelegramMessageLink.Own,
            });
            _ownLines.RemoveAt(own);
            await db.SaveChangesAsync(_cts.Token);
            return;
        }
        if (link?.ContentHash == TelegramMessageLink.Own) return;

        if (!visible)
        {
            if (link is null) return;
            if (link.TelegramMessageId > 0)
            {
                try { await SendAsync(chat => _d.Api.DeleteMessageAsync(chat, link.TelegramMessageId, _cts.Token)); }
                catch (TelegramApiException ex) { _d.Logger.LogDebug("Telegram: could not delete {Id} in {ChatId} — {Message}", link.TelegramMessageId, ChatId, ex.Message); }
            }
            db.TelegramMessageLinks.Remove(link);
            await db.SaveChangesAsync(_cts.Token);
            return;
        }

        var (html, keyboard) = await RenderAsync(db, message!, viewer, personalOnly ? gameId : null);
        var hash = Hash(html, keyboard);
        if (link is not null)
        {
            if (link.ContentHash == hash) return;
            await EditAsync(link.TelegramMessageId, html, keyboard);
            link.ContentHash = hash;
        }
        else
        {
            var sent = await SendTextAsync(html, keyboard, silent: message!.Type is "System" or "Combat");
            db.TelegramMessageLinks.Add(new TelegramMessageLink
            {
                ChatId = ChatId, ThreadId = ThreadKey, Kind = TelegramLinkKind.Message, RefId = messageId,
                TelegramMessageId = sent.MessageId, ContentHash = hash,
            });
        }
        await db.SaveChangesAsync(_cts.Token);
    }

    private async Task<(string Html, IReadOnlyList<IReadOnlyList<TgButton>>? Keyboard)> RenderAsync(
        AppDbContext db, Message m, Player? viewer, Guid? foreignGameId)
    {
        var sender = m.PlayerId is { } pid ? await NameOfAsync(db, pid) : null;
        var target = m.WhisperToId is { } to
            ? to == viewer?.Id ? "you" : await NameOfAsync(db, to)
            : null;
        var gameTitle = foreignGameId is { } gid
            ? await db.Games.AsNoTracking().Where(g => g.Id == gid).Select(g => g.Name).FirstOrDefaultAsync(_cts.Token)
            : null;

        var open = false;
        if (viewer is not null && m.WhisperToId == viewer.Id && PromptTypes.Contains(m.Type) && ToolCallId(m) is { } toolCallId)
            open = await db.GMToolCalls.AsNoTracking().AnyAsync(t => t.Id == toolCallId
                && (t.Status == GMToolCallStatus.AwaitingConfirmation || t.Status == GMToolCallStatus.AwaitingReroll), _cts.Token);

        var html = RenderMessage(m, new MessageContext(sender, target, gameTitle, open));
        return (html, PromptKeyboard(m, open));
    }

    private static Guid? ToolCallId(Message m) =>
        m.Metadata.ValueKind == System.Text.Json.JsonValueKind.Object
        && m.Metadata.TryGetProperty("toolCallId", out var id) && Guid.TryParse(id.GetString(), out var g) ? g : null;

    /// <summary>
    /// How the table knows a player: their character, with the account in brackets when it differs (as the web chat
    /// shows it).
    /// </summary>
    internal static async Task<string?> NameOfAsync(AppDbContext db, Guid playerId)
    {
        var p = await db.Players.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.Id == playerId)
            .Select(x => new { x.CharacterName, x.User.DisplayName, Character = x.Character != null ? x.Character.Name : null })
            .FirstOrDefaultAsync();
        if (p is null) return null;
        var character = !string.IsNullOrWhiteSpace(p.Character) ? p.Character.Trim()
            : !string.IsNullOrWhiteSpace(p.CharacterName) ? p.CharacterName.Trim() : null;
        if (character is null) return p.DisplayName;
        return string.IsNullOrWhiteSpace(p.DisplayName) || p.DisplayName == character ? character : $"{character} ({p.DisplayName})";
    }

    // ── Combat card ───────────────────────────────────────────────────────────

    /// <summary>
    /// Draws the fight as one message, edited as it changes (pinned in groups where the bot may pin). When the fight
    /// ends the card says so and is unpinned. <paramref name="fresh"/> (/combat) posts it again at the bottom.
    /// </summary>
    public async Task SyncCombatCardAsync(IServiceProvider sp, bool fresh)
    {
        if (FollowedGameId is not { } gameId) return;
        var db = sp.GetRequiredService<AppDbContext>();
        var combats = sp.GetRequiredService<ICombatTracker>();

        // Cards of fights that are over, not yet drawn as finished.
        var cards = await db.TelegramMessageLinks
            .Where(l => l.ChatId == ChatId && l.ThreadId == ThreadKey && l.Kind == TelegramLinkKind.CombatCard)
            .ToListAsync(_cts.Token);
        var active = await combats.GetActiveAsync(gameId, _cts.Token);
        foreach (var card in cards.Where(c => c.RefId != active?.Id))
        {
            var ended = await db.Combats.AsNoTracking().Include(c => c.Participants)
                .FirstOrDefaultAsync(c => c.Id == card.RefId && c.GameId == gameId, _cts.Token);
            if (ended is null) continue;
            var dto = combats.ToDto(ended) with { Status = nameof(CombatStatus.Finished) };
            var html = CombatCard(dto);
            var hash = Hash(html, null);
            if (card.ContentHash == hash) continue;
            await EditAsync(card.TelegramMessageId, html, null);
            if (!IsPrivate) await TryAsync(() => _d.Api.UnpinChatMessageAsync(ChatId, card.TelegramMessageId, _cts.Token));
            card.ContentHash = hash;
        }

        if (active is not null)
        {
            var html = CombatCard(combats.ToDto(active));
            var hash = Hash(html, null);
            var card = cards.FirstOrDefault(c => c.RefId == active.Id);
            if (card is not null && !fresh)
            {
                if (card.ContentHash != hash) await EditAsync(card.TelegramMessageId, html, null);
            }
            else
            {
                var sent = await SendTextAsync(html, silent: true);
                if (!IsPrivate) await TryAsync(() => _d.Api.PinChatMessageAsync(ChatId, sent.MessageId, _cts.Token));
                if (card is null)
                {
                    card = new TelegramMessageLink { ChatId = ChatId, ThreadId = ThreadKey, Kind = TelegramLinkKind.CombatCard, RefId = active.Id };
                    db.TelegramMessageLinks.Add(card);
                }
                card.TelegramMessageId = sent.MessageId;
            }
            card.ContentHash = hash;
        }
        else if (fresh)
        {
            await SendTextAsync("🕊 Nobody is fighting right now.");
        }
        await db.SaveChangesAsync(_cts.Token);
    }

    /// <summary>Pinning needs admin rights the bot may not have; that is not an error worth more than a log line.</summary>
    private async Task TryAsync(Func<Task> call)
    {
        try { await call(); }
        catch (TelegramApiException ex) { _d.Logger.LogDebug("Telegram: chat {ChatId} — {Message}", ChatId, ex.Message); }
    }

    // ── Sending ───────────────────────────────────────────────────────────────

    private TimeSpan MinGap => IsPrivate ? TimeSpan.FromSeconds(1.05) : TimeSpan.FromSeconds(3.1);

    /// <summary>
    /// Every send goes through here: waits for the chat's pacing gap, follows a group→supergroup move once, and ends
    /// the binding when the chat or topic is gone for good.
    /// </summary>
    private async Task<T> SendAsync<T>(Func<long, Task<T>> call)
    {
        var wait = _lastSend + MinGap - DateTime.UtcNow;
        if (wait > TimeSpan.Zero) await Task.Delay(wait, _cts.Token);
        try
        {
            return await call(ChatId);
        }
        catch (TelegramApiException ex) when (ex.MigrateToChatId is long newChatId)
        {
            await WithScopeAsync(sp => MoveChatAsync(sp, newChatId));
            return await call(ChatId);
        }
        catch (TelegramApiException ex) when (ex.ChatGone)
        {
            await WithScopeAsync(sp => EndAsync(sp, $"Telegram refused to deliver: {ex.Description}"));
            throw new ChatGoneException();
        }
        finally
        {
            _lastSend = DateTime.UtcNow;
        }
    }

    public Task<TgMessage> SendTextAsync(
        string html, IReadOnlyList<IReadOnlyList<TgButton>>? keyboard = null, int? replyTo = null, bool silent = false) =>
        SendAsync(chat => _d.Api.SendMessageAsync(chat, ThreadId, html, keyboard, replyTo, silent, _cts.Token));

    private async Task EditAsync(int messageId, string html, IReadOnlyList<IReadOnlyList<TgButton>>? keyboard)
    {
        try { await SendAsync(chat => _d.Api.EditMessageTextAsync(chat, messageId, html, keyboard, _cts.Token)); }
        catch (TelegramApiException ex) when (ex.NotModified) { }
        catch (TelegramApiException ex) when (ex.MessageMissing)
        {
            // Deleted by someone in the chat, or too old to edit: left as it is.
            _d.Logger.LogDebug("Telegram: message {Id} in {ChatId} can't be edited ({Message}); not redrawn", messageId, ChatId, ex.Description);
        }
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    /// <summary>Re-reads which game a private chat follows (or the group's binding) and the games its user plays in.</summary>
    public Task ReloadAsync() => WithScopeAsync(async sp =>
    {
        var db = sp.GetRequiredService<AppDbContext>();
        if (IsPrivate)
            _binding = await db.TelegramBindings.AsNoTracking()
                .FirstOrDefaultAsync(b => b.ChatId == ChatId && b.ThreadId == null && b.RevokedAt == null && b.UserId == UserId, _cts.Token);
        else if (_binding is { } current)
        {
            var fresh = await db.TelegramBindings.AsNoTracking().FirstOrDefaultAsync(b => b.Id == current.Id, _cts.Token);
            if (fresh is null || !fresh.IsActive) { _cts.Cancel(); return; }
            _binding = fresh;
        }
        await RefreshMemberGamesAsync(sp);
    });

    private async Task RefreshMemberGamesAsync(IServiceProvider sp)
    {
        _memberGamesAt = DateTime.UtcNow;
        if (UserId is not { } uid) return;
        var db = sp.GetRequiredService<AppDbContext>();
        var games = await db.Players.AsNoTracking().Where(p => p.UserId == uid).Select(p => p.GameId).ToListAsync(_cts.Token);
        _memberGames = [.. games];
    }

    private async Task MoveChatAsync(IServiceProvider sp, long newChatId)
    {
        var db = sp.GetRequiredService<AppDbContext>();
        await db.TelegramBindings.Where(b => b.ChatId == ChatId)
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.ChatId, newChatId), _cts.Token);
        await db.TelegramMessageLinks.Where(l => l.ChatId == ChatId)
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.ChatId, newChatId), _cts.Token);
        _d.Logger.LogInformation("Telegram: chat {Old} moved to {New}.", ChatId, newChatId);
        ChatId = newChatId;
    }

    /// <summary>Telegram will not deliver here any more: the binding ends (an account stays linked).</summary>
    private async Task EndAsync(IServiceProvider sp, string reason)
    {
        var db = sp.GetRequiredService<AppDbContext>();
        var bindings = await db.TelegramBindings
            .Where(b => b.ChatId == ChatId && b.ThreadId == ThreadId && b.RevokedAt == null).ToListAsync(_cts.Token);
        foreach (var b in bindings)
        {
            b.RevokedAt = DateTimeOffset.UtcNow;
            b.RevokedReason = Clip(reason, 300);
        }
        await db.SaveChangesAsync(_cts.Token);
        _d.Logger.LogWarning("Telegram: chat {ChatId} ended — {Reason}", ChatId, reason);
        _cts.Cancel();
    }

    private async Task WelcomeAsync(IServiceProvider sp)
    {
        if (FollowedGameId is not { } gameId) return;
        var db = sp.GetRequiredService<AppDbContext>();
        var game = await db.Games.AsNoTracking().FirstOrDefaultAsync(g => g.Id == gameId, _cts.Token);
        if (game is null) return;
        var where = IsPrivate ? "chat" : ThreadId is null ? "group" : "topic";
        var bot = _d.Bot.Username is { } u ? "@" + u : "the bot";
        var text = IsPrivate
            ? $"▶ This chat now follows <b>{H(game.Name)}</b>. Write here to speak in character; /ooc for table talk, /ask to ask the GM privately, /w to whisper. /help lists the rest."
            : $"✅ This {where} now plays <b>{H(game.Name)}</b>.\n\n" +
              $"Players: link your account once (in ADnD: User Settings → Telegram), then just write here to speak in character. " +
              $"/ooc for table talk. Roll prompts and whispers come to your private chat with {H(bot)} — open it and press Start once.\n\n" +
              "The bot has to read the messages here: turn its privacy mode off (@BotFather → /setprivacy) or make it an admin.";
        await SendTextAsync(text);
        await SyncCombatCardAsync(sp, fresh: false);
    }
}

/// <summary>The chat is gone for good (bot removed, blocked, topic deleted): its worker stops.</summary>
public sealed class ChatGoneException : Exception;
