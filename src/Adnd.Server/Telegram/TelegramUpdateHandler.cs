using Adnd.Server.Data;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Microsoft.EntityFrameworkCore;
using static Adnd.Server.Telegram.TelegramFormat;

namespace Adnd.Server.Telegram;

/// <summary>
/// Handles what people send the bot, one update at a time. A player always acts through their linked ADnD account
/// and the same services as the web chat (<see cref="ITableChatService"/>, <see cref="IRollAnswerService"/>), so the
/// rules are the web's: a member of the game, a living character to speak in character, a roll prompt only for the
/// player it was asked of. Players never roll here either — a prompt's button answers the GM's request.
/// <list type="bullet">
/// <item>In a group or topic that plays a game, a plain message from a linked player is an in-character line (and
/// starts a GM turn, as the web chat's send does); <c>((</c> or /ooc makes it table talk.</item>
/// <item>In a private chat that follows a game, the same; a reply to a player's whisper whispers back.</item>
/// <item>Buttons answer roll prompts (<c>r:</c>) and pick the game to play (<c>g:</c>).</item>
/// </list>
/// </summary>
public sealed class TelegramUpdateHandler(
    IServiceScopeFactory scopes,
    TelegramApi api,
    TelegramBotState bot,
    TelegramDispatcher dispatcher,
    ILogger<TelegramUpdateHandler> logger)
{
    private static readonly TimeSpan HintEvery = TimeSpan.FromMinutes(10);

    public static readonly (string Command, string Description)[] Commands =
    [
        ("games", "Your games — pick one to play in this chat"),
        ("ooc", "Say something out of character"),
        ("ask", "Ask the GM a private question"),
        ("w", "Whisper to a player: /w Name text"),
        ("sheet", "Your character sheet"),
        ("party", "The party"),
        ("combat", "The fight as it stands"),
        ("stop", "Stop playing a game in this private chat"),
        ("unbind", "Disconnect this group from its game"),
        ("unlink", "Unlink your ADnD account"),
        ("help", "What the bot can do"),
    ];

    public async Task HandleAsync(TgUpdate update, CancellationToken ct)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var sp = scope.ServiceProvider;
            if (update.CallbackQuery is { } callback) await OnCallbackAsync(sp, callback, ct);
            else if (update.MyChatMember is { } member) await OnMembershipAsync(sp, member);
            else if (update.Message is { } message) await OnMessageAsync(sp, message, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Telegram: update {UpdateId} failed — {Message}", update.UpdateId, ex.Message);
        }
    }

    // ── Messages ──────────────────────────────────────────────────────────────

    private async Task OnMessageAsync(IServiceProvider sp, TgMessage message, CancellationToken ct)
    {
        var db = sp.GetRequiredService<AppDbContext>();
        if (message.MigrateToChatId is long newChatId)
        {
            await db.TelegramBindings.Where(b => b.ChatId == message.Chat.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(b => b.ChatId, newChatId), ct);
            await db.TelegramMessageLinks.Where(l => l.ChatId == message.Chat.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(l => l.ChatId, newChatId), ct);
            // A group became a supergroup: same table, new chat id.
            foreach (var old in await db.TelegramBindings.AsNoTracking().Where(b => b.ChatId == newChatId && b.RevokedAt == null).ToListAsync(ct))
            {
                dispatcher.Detach(message.Chat.Id, old.ThreadId);
                dispatcher.AttachGroup(old, welcome: false);
            }
            return;
        }

        var text = (message.Text ?? message.Caption)?.Trim();
        if (string.IsNullOrEmpty(text) || message.From is not { IsBot: false } from) return;

        var links = sp.GetRequiredService<TelegramLinkService>();
        var user = await links.LinkedUserAsync(from.Id);
        var isPrivate = message.Chat.Type == "private";
        if (isPrivate && user is not null && dispatcher.Worker(message.Chat.Id, null) is null)
            dispatcher.EnsurePrivate(user.Id, message.Chat.Id);

        var binding = await db.TelegramBindings.AsNoTracking().Include(b => b.Game)
            .FirstOrDefaultAsync(b => b.ChatId == message.Chat.Id && b.ThreadId == message.TopicId && b.RevokedAt == null, ct);
        // A private chat only ever follows a game for its own linked user.
        if (binding is { IsPrivate: true } && binding.UserId != user?.Id) binding = null;

        if (ParseCommand(text, bot.Username) is { } command)
            await OnCommandAsync(sp, message, user, binding, command.Command, command.Args, ct);
        else if (binding is not null)
            await OnLineAsync(sp, message, user, binding, text, ct);
        else if (isPrivate)
            await ReplyAsync(message, user is null ? NotLinked : "Pick a game to play here first: /games.");
        // In a group that plays no game, plain messages are not the bot's business.
    }

    private async Task OnCommandAsync(IServiceProvider sp, TgMessage message, User? user, TelegramBinding? binding,
        string command, string args, CancellationToken ct)
    {
        var links = sp.GetRequiredService<TelegramLinkService>();
        var isPrivate = message.Chat.Type == "private";
        switch (command)
        {
            case "start" when args.StartsWith(TelegramLinkService.AccountPrefix, StringComparison.Ordinal):
                await ReplyAsync(message, await links.LinkAsync(message, args[TelegramLinkService.AccountPrefix.Length..]));
                break;
            case "start" when TelegramLinkService.LooksLikeCode(args):
            case "bind":
                if (await links.BindGroupAsync(message, args) is { } bindError) await ReplyAsync(message, bindError);
                break;
            case "start":
                await ReplyAsync(message, Greeting(user, binding, isPrivate));
                break;
            case "link":
                await ReplyAsync(message, await links.LinkAsync(message, args));
                break;
            case "unlink":
                if (user is null) await ReplyAsync(message, "ℹ️ This Telegram account isn't linked.");
                else if (!isPrivate) await ReplyAsync(message, "🔒 Send /unlink in your private chat with the bot.");
                else
                {
                    await links.UnlinkAsync(user);
                    await ReplyAsync(message, "✅ Unlinked. Link again from ADnD: User Settings → Telegram.");
                }
                break;
            case "games" or "play":
                await GamesAsync(sp, message, user, binding, isPrivate, ct);
                break;
            case "stop" when isPrivate:
                if (user is null) await ReplyAsync(message, NotLinked);
                else await ReplyAsync(message, await links.UnfollowAsync(user, message.Chat.Id)
                    ? "⏹ This chat no longer plays a game. Roll prompts and whispers still come here. /games to pick one again."
                    : "ℹ️ This chat plays no game. /games to pick one.");
                break;
            case "unbind":
                await UnbindAsync(sp, message, user, binding);
                break;
            case "ooc" or "ask" or "w":
                if (binding is null)
                    await ReplyAsync(message, isPrivate ? (user is null ? NotLinked : "Pick a game to play here first: /games.") : "ℹ️ This chat plays no game.");
                else if (args.Length == 0)
                    await ReplyAsync(message, command switch
                    {
                        "ooc" => "Usage: <code>/ooc text</code>",
                        "ask" => "Usage: <code>/ask question</code> — the GM answers you privately.",
                        _ => "Usage: <code>/w Name text</code>",
                    });
                else if (command == "w" && !isPrivate)
                    await ReplyAsync(message, "🔒 Everyone here would read it: whisper in your private chat with the bot.");
                else
                    await OnLineAsync(sp, message, user, binding, args, ct, command);
                break;
            case "sheet":
                await SheetAsync(sp, message, user, binding, ct);
                break;
            case "party":
                await PartyAsync(sp, message, binding, ct);
                break;
            case "combat":
                if (binding is null) await ReplyAsync(message, "ℹ️ This chat plays no game.");
                else dispatcher.Enqueue(message.Chat.Id, binding.ThreadId, async w =>
                {
                    using var scope = scopes.CreateScope();
                    await w.SyncCombatCardAsync(scope.ServiceProvider, fresh: true);
                });
                break;
            case "help":
                await ReplyAsync(message, Help(isPrivate));
                break;
            default:
                if (isPrivate) await ReplyAsync(message, "I don't know that command. /help lists what I can do.");
                break;
        }
    }

    /// <summary>
    /// A line at the table: in character (also starting a GM turn), or <paramref name="command"/> ooc / ask / w.
    /// Refusals — not a member, no living character — are told to the sender.
    /// </summary>
    private async Task OnLineAsync(IServiceProvider sp, TgMessage message, User? user, TelegramBinding binding,
        string text, CancellationToken ct, string? command = null)
    {
        if (user is null)
        {
            await HintAsync(message);
            return;
        }
        var chat = sp.GetRequiredService<ITableChatService>();
        var db = sp.GetRequiredService<AppDbContext>();
        var gameId = binding.GameId;
        try
        {
            var player = await chat.RequireMemberAsync(gameId, user.Id, ct);
            var worker = dispatcher.Worker(message.Chat.Id, binding.ThreadId);

            if (command is null && text.StartsWith("((", StringComparison.Ordinal))
            {
                command = "ooc";
                text = text.TrimStart('(').TrimEnd(')').Trim();
                if (text.Length == 0) return;
            }

            switch (command)
            {
                case "ooc":
                    worker?.ExpectOwnLine(player.Id, text, message.MessageId);
                    await chat.SayAsync(gameId, user.Id, text, isOOC: true, ct);
                    return;
                case "ask":
                    worker?.ExpectOwnLine(player.Id, text, message.MessageId);
                    await chat.AskGmAsync(gameId, user.Id, text, ct);
                    await ReactAsync(message, "👀");
                    return;
                case "w":
                    if (await ResolveWhisperAsync(db, gameId, player, text, ct) is not { } whisper)
                    {
                        await ReplyAsync(message, $"❌ Whisper to whom? The players: {H(string.Join(", ", await PlayerNamesAsync(db, gameId, player.Id, ct)))}.");
                        return;
                    }
                    worker?.ExpectOwnLine(player.Id, whisper.Text, message.MessageId);
                    await chat.WhisperAsync(gameId, user.Id, whisper.TargetPlayerId, whisper.Text, isOOC: false, ct);
                    return;
            }

            // A reply to a player's whisper (in the private chat) whispers back.
            if (binding.IsPrivate && message.ReplyToMessage is { } replied
                && await WhisperSenderAsync(db, message.Chat.Id, replied.MessageId, gameId, player.Id, ct) is { } sender)
            {
                worker?.ExpectOwnLine(player.Id, text, message.MessageId);
                await chat.WhisperAsync(gameId, user.Id, sender, text, isOOC: false, ct);
                return;
            }

            worker?.ExpectOwnLine(player.Id, text, message.MessageId);
            await chat.SayAsync(gameId, user.Id, text, isOOC: false, ct);
            await chat.NarrateAsync(gameId, user.Id, text, ct);
            await ReactAsync(message, "👀");
        }
        catch (TableChatException ex)
        {
            await ReplyAsync(message, $"❌ {H(ex.Message)}" + (text.Length > 0 ? " Use /ooc for table talk." : ""));
        }
    }

    /// <summary>"Name text" → the player whose character (or account) name the text starts with — the longest match.</summary>
    private static async Task<(Guid TargetPlayerId, string Text)?> ResolveWhisperAsync(
        AppDbContext db, Guid gameId, Player sender, string args, CancellationToken ct)
    {
        var players = await db.Players.AsNoTracking().Where(p => p.GameId == gameId && p.Id != sender.Id)
            .Select(p => new { p.Id, p.CharacterName, p.User.DisplayName, Character = p.Character != null ? p.Character.Name : null })
            .ToListAsync(ct);
        var best = players
            .SelectMany(p => new[] { p.Character, p.CharacterName, p.DisplayName }
                .Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => (p.Id, Name: n!.Trim())))
            .Where(c => args.StartsWith(c.Name, StringComparison.OrdinalIgnoreCase)
                        && (args.Length == c.Name.Length || char.IsWhiteSpace(args[c.Name.Length]) || args[c.Name.Length] is ':' or ','))
            .OrderByDescending(c => c.Name.Length)
            .FirstOrDefault();
        if (best.Name is null) return null;
        var text = args[best.Name.Length..].TrimStart(':', ',', ' ').Trim();
        return text.Length == 0 ? null : (best.Id, text);
    }

    private static async Task<List<string>> PlayerNamesAsync(AppDbContext db, Guid gameId, Guid except, CancellationToken ct)
    {
        var ids = await db.Players.AsNoTracking().Where(p => p.GameId == gameId && p.Id != except).Select(p => p.Id).ToListAsync(ct);
        var names = new List<string>();
        foreach (var id in ids)
            if (await ChatWorker.NameOfAsync(db, id) is { } n) names.Add(n);
        return names.Count > 0 ? names : ["(nobody else)"];
    }

    /// <summary>The player who whispered the message the bot posted as <paramref name="telegramMessageId"/>, if it was a whisper to <paramref name="me"/>.</summary>
    private static async Task<Guid?> WhisperSenderAsync(AppDbContext db, long chatId, int telegramMessageId, Guid gameId, Guid me, CancellationToken ct)
    {
        var refId = await db.TelegramMessageLinks.AsNoTracking()
            .Where(l => l.ChatId == chatId && l.TelegramMessageId == telegramMessageId && l.Kind == TelegramLinkKind.Message)
            .Select(l => (Guid?)l.RefId).FirstOrDefaultAsync(ct);
        if (refId is null) return null;
        return await db.Messages.AsNoTracking()
            .Where(m => m.Id == refId && m.Session.GameId == gameId && m.WhisperToId == me && m.WhisperFromId != null && m.WhisperFromId != me)
            .Select(m => m.WhisperFromId).FirstOrDefaultAsync(ct);
    }

    /// <summary>An unlinked person wrote at a table: say once in a while how to join in.</summary>
    private async Task HintAsync(TgMessage message)
    {
        var worker = dispatcher.Worker(message.Chat.Id, message.TopicId);
        var from = message.From!.Id;
        if (worker is not null)
        {
            if (worker.LastHint.TryGetValue(from, out var last) && DateTime.UtcNow - last < HintEvery) return;
            worker.LastHint[from] = DateTime.UtcNow;
        }
        await ReplyAsync(message, message.Chat.Type == "private" ? NotLinked :
            $"ℹ️ {H(message.From.Display)}, to play from here link your ADnD account: open a private chat with " +
            $"{H(bot.Username is { } u ? "@" + u : "the bot")} and follow /start.");
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    private async Task GamesAsync(IServiceProvider sp, TgMessage message, User? user, TelegramBinding? binding, bool isPrivate, CancellationToken ct)
    {
        if (!isPrivate)
        {
            await ReplyAsync(message, binding is null ? "ℹ️ This chat plays no game." : $"🗺 This chat plays <b>{H(binding.Game.Name)}</b>.");
            return;
        }
        if (user is null)
        {
            await ReplyAsync(message, NotLinked);
            return;
        }
        var db = sp.GetRequiredService<AppDbContext>();
        var games = await db.Players.AsNoTracking().Where(p => p.UserId == user.Id)
            .Select(p => new { p.GameId, p.Game.Name, p.Game.Status }).OrderBy(g => g.Name).ToListAsync(ct);
        if (games.Count == 0)
        {
            await ReplyAsync(message, "You aren't in any game yet. Join one in ADnD with its invite code.");
            return;
        }
        var rows = games.Select(g => (IReadOnlyList<TgButton>)[new TgButton(
            (binding?.GameId == g.GameId ? "▶ " : "") + Clip(g.Name, 50), $"g:{g.GameId:N}")]).ToList();
        await api.SendMessageAsync(message.Chat.Id, message.TopicId,
            "🗺 <b>Your games</b> — pick one to play in this chat:", rows, ct: ct);
    }

    private async Task UnbindAsync(IServiceProvider sp, TgMessage message, User? user, TelegramBinding? binding)
    {
        if (binding is null)
        {
            await ReplyAsync(message, "ℹ️ This chat plays no game.");
            return;
        }
        var links = sp.GetRequiredService<TelegramLinkService>();
        if (binding.IsPrivate)
        {
            await links.UnfollowAsync(user!, message.Chat.Id);
            await ReplyAsync(message, "⏹ This chat no longer plays a game. /games to pick one again.");
            return;
        }
        // The game's creator, or whoever runs the group.
        var allowed = user?.Id == binding.Game.CreatorId || await IsChatAdminAsync(message);
        if (!allowed)
        {
            await ReplyAsync(message, "🔒 Only the game's creator or a group admin can disconnect it.");
            return;
        }
        var db = sp.GetRequiredService<AppDbContext>();
        var tracked = await db.TelegramBindings.FirstAsync(b => b.Id == binding.Id);
        await links.RevokeAsync(tracked, $"disconnected by {message.From!.Display}", tellChat: false);
        await ReplyAsync(message, $"🔌 This chat no longer plays <b>{H(binding.Game.Name)}</b>.");
    }

    private async Task<bool> IsChatAdminAsync(TgMessage message)
    {
        // An anonymous admin posts as the group itself.
        if (message.SenderChat?.Id == message.Chat.Id) return true;
        try
        {
            var member = await api.GetChatMemberAsync(message.Chat.Id, message.From!.Id);
            return member.Status is "creator" or "administrator";
        }
        catch (TelegramApiException ex)
        {
            logger.LogInformation("Telegram: getChatMember failed in {ChatId}: {Message}", message.Chat.Id, ex.Message);
            return false;
        }
    }

    private async Task SheetAsync(IServiceProvider sp, TgMessage message, User? user, TelegramBinding? binding, CancellationToken ct)
    {
        if (binding is null || user is null)
        {
            await ReplyAsync(message, user is null ? NotLinked : "ℹ️ This chat plays no game.");
            return;
        }
        var db = sp.GetRequiredService<AppDbContext>();
        var character = await db.Characters.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Player.GameId == binding.GameId && c.Player.UserId == user.Id, ct);
        await ReplyAsync(message, character is null
            ? "You have no character in this game yet. Create one in ADnD."
            : Sheet(character));
    }

    private async Task PartyAsync(IServiceProvider sp, TgMessage message, TelegramBinding? binding, CancellationToken ct)
    {
        if (binding is null)
        {
            await ReplyAsync(message, "ℹ️ This chat plays no game.");
            return;
        }
        var db = sp.GetRequiredService<AppDbContext>();
        var party = await db.Characters.AsNoTracking()
            .Where(c => c.Player.GameId == binding.GameId)
            .Select(c => new { c.Name, c.Race, c.Class, c.Level, c.CurrentHP, c.MaxHP, c.Player.User.DisplayName, c.Conditions })
            .OrderBy(c => c.Name).ToListAsync(ct);
        if (party.Count == 0)
        {
            await ReplyAsync(message, "Nobody has a character yet.");
            return;
        }
        var lines = party.Select(c =>
        {
            var dead = c.Conditions.ValueKind == System.Text.Json.JsonValueKind.Array
                       && c.Conditions.EnumerateArray().Any(x => x.ValueKind == System.Text.Json.JsonValueKind.String && x.GetString() == CharacterStateService.Dead);
            return $"{(dead ? "💀" : "🛡")} <b>{H(c.Name)}</b> — {H(c.Race)} {H(c.Class)} {c.Level}, {c.CurrentHP}/{c.MaxHP} HP <i>({H(c.DisplayName)})</i>";
        });
        await ReplyAsync(message, $"🧑‍🤝‍🧑 <b>{H(binding.Game.Name)}</b>\n" + string.Join('\n', lines));
    }

    // ── Buttons ───────────────────────────────────────────────────────────────

    private async Task OnCallbackAsync(IServiceProvider sp, TgCallbackQuery callback, CancellationToken ct)
    {
        var data = callback.Data ?? "";
        var links = sp.GetRequiredService<TelegramLinkService>();
        var user = await links.LinkedUserAsync(callback.From.Id);
        if (user is null)
        {
            await AnswerAsync(callback, "Link your ADnD account first: User Settings → Telegram.", alert: true);
            return;
        }

        var parts = data.Split(':');
        if (parts.Length >= 2 && parts[0] == "g" && Guid.TryParseExact(parts[1], "N", out var gameId))
        {
            if (callback.Message?.Chat is not { Type: "private" } chat)
            {
                await AnswerAsync(callback, "Pick a game in your private chat with the bot.", alert: true);
                return;
            }
            var error = await links.FollowAsync(user, chat.Id, gameId);
            await AnswerAsync(callback, error is null ? "▶ Playing it here" : StripTags(error), alert: error is not null);
            return;
        }

        if (parts.Length >= 3 && parts[0] == "r" && Guid.TryParseExact(parts[1], "N", out var toolCallId))
        {
            var answers = sp.GetRequiredService<IRollAnswerService>();
            RollAnswer answer;
            switch (parts[2])
            {
                case "c":
                    answer = await answers.ConfirmAsync(toolCallId, user.Id, approved: true, reason: null, ct);
                    break;
                case "d":
                    answer = await answers.ConfirmAsync(toolCallId, user.Id, approved: false, reason: null, ct);
                    break;
                case "k":
                    answer = await answers.RerollAsync(toolCallId, user.Id, null, ct);
                    break;
                case "o" when parts.Length >= 4 && int.TryParse(parts[3], out var index):
                    var db = sp.GetRequiredService<AppDbContext>();
                    var result = await db.GMToolCalls.AsNoTracking().Where(t => t.Id == toolCallId).Select(t => t.Result).FirstOrDefaultAsync(ct);
                    var featureId = RollAnswerService.OfferedFeatureId(result, index);
                    answer = featureId is null
                        ? new RollAnswer(false, "That ability was not offered for this roll.")
                        : await answers.RerollAsync(toolCallId, user.Id, featureId, ct);
                    break;
                default:
                    await AnswerAsync(callback, null);
                    return;
            }
            // The prompt message is redrawn from the game's own event once the roll is made.
            await AnswerAsync(callback, answer.Ok ? (parts[2] == "d" ? "Declined" : "🎲") : answer.Error, alert: !answer.Ok);
            return;
        }

        await AnswerAsync(callback, null);
    }

    private async Task AnswerAsync(TgCallbackQuery callback, string? text, bool alert = false)
    {
        try { await api.AnswerCallbackQueryAsync(callback.Id, text, alert); }
        catch (TelegramApiException ex) { logger.LogDebug("Telegram: answerCallbackQuery — {Message}", ex.Message); }
    }

    private static string StripTags(string html) =>
        System.Net.WebUtility.HtmlDecode(System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", ""));

    // ── Membership ────────────────────────────────────────────────────────────

    /// <summary>The bot was removed from a group, or blocked in a private chat: its bindings end.</summary>
    private async Task OnMembershipAsync(IServiceProvider sp, TgChatMemberUpdated update)
    {
        if (update.NewChatMember.Status is not ("left" or "kicked")) return;
        var db = sp.GetRequiredService<AppDbContext>();
        var links = sp.GetRequiredService<TelegramLinkService>();
        var bindings = await db.TelegramBindings.Where(b => b.ChatId == update.Chat.Id && b.RevokedAt == null).ToListAsync();
        foreach (var binding in bindings)
            await links.RevokeAsync(binding, update.Chat.Type == "private" ? "the bot was blocked" : "the bot was removed", tellChat: false);
        if (update.Chat.Type == "private") dispatcher.Detach(update.Chat.Id, null);
    }

    // ── Replies ───────────────────────────────────────────────────────────────

    private const string NotLinked =
        "👋 To play from Telegram, link your ADnD account: in ADnD open <b>User Settings → Telegram</b> and press <b>Link</b>, then open the link it gives you.";

    private static string Greeting(User? user, TelegramBinding? binding, bool isPrivate)
    {
        if (!isPrivate)
            return binding is null
                ? "👋 To play a game here, its creator makes a code in Game Settings → Telegram and sends <code>/bind CODE</code> here."
                : $"🗺 This chat plays <b>{H(binding.Game.Name)}</b>. Linked players: just write to speak in character. /help";
        if (user is null) return NotLinked;
        return $"👋 Hello, <b>{H(user.DisplayName)}</b>. " + (binding is null
            ? "/games to pick a game to play here. Roll prompts and whispers from all your games come here anyway."
            : $"This chat plays <b>{H(binding.Game.Name)}</b> — write to speak in character. /help");
    }

    private static string Help(bool isPrivate) =>
        "🎲 <b>ADnD at the table</b>\n\n" +
        "Write a message to speak in character — the GM answers. Start it with <code>((</code> or use /ooc for table talk.\n" +
        "/ask <i>question</i> — ask the GM privately (the answer comes to you alone)\n" +
        "/w <i>Name text</i> — whisper to a player (in the private chat)\n" +
        "/sheet — your character · /party — the party · /combat — the fight\n" +
        (isPrivate
            ? "/games — pick the game this chat plays · /stop — stop playing it here · /unlink — unlink your account\n\n" +
              "Roll prompts from all your games come here with their buttons; you never roll yourself — the GM does, when you press Roll."
            : "/unbind — disconnect this chat from its game (creator or admin)\n\n" +
              "Roll prompts and whispers go to each player's private chat with the bot.");

    private async Task ReplyAsync(TgMessage message, string html)
    {
        try { await api.SendMessageAsync(message.Chat.Id, message.TopicId, html, replyTo: message.MessageId); }
        catch (TelegramApiException ex) { logger.LogInformation("Telegram: reply in {ChatId} failed — {Message}", message.Chat.Id, ex.Message); }
    }

    private async Task ReactAsync(TgMessage message, string emoji)
    {
        try { await api.SetReactionAsync(message.Chat.Id, message.MessageId, emoji); }
        catch (TelegramApiException ex) { logger.LogDebug("Telegram: reaction in {ChatId} failed — {Message}", message.Chat.Id, ex.Message); }
    }
}
