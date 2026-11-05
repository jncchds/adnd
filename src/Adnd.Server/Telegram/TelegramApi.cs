using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Adnd.Server.Telegram;

/// <summary>
/// A thin Bot API client: JSON calls and a retry on 429 (flood control) that waits the <c>retry_after</c> Telegram
/// asks for. Methods the bot needs are wrapped below; anything else goes through <see cref="CallAsync{T}"/>. Payloads
/// are anonymous objects with Bot API (snake_case) names; nulls are left out. Its HttpClient never logs requests:
/// the token is in every URL.
/// </summary>
public sealed class TelegramApi(IHttpClientFactory httpFactory, TelegramOptions options, ILogger<TelegramApi> logger)
{
    public const string HttpClientName = "telegram";
    private const int MaxRetries = 3;
    private static readonly TimeSpan MaxRetryWait = TimeSpan.FromSeconds(60);

    internal static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
    };

    private string MethodUrl(string method) => $"{options.ApiUrl}/bot{options.BotToken}/{method}";

    /// <summary>Calls a Bot API method with a JSON body and returns its result.</summary>
    public async Task<T> CallAsync<T>(string method, object? payload, CancellationToken ct = default, TimeSpan? timeout = null)
    {
        var body = JsonSerializer.Serialize(payload ?? new { }, Json);
        for (var attempt = 0; ; attempt++)
        {
            var http = httpFactory.CreateClient(HttpClientName);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeout ?? TimeSpan.FromSeconds(60));
            using var content = new StringContent(body, Encoding.UTF8, "application/json");
            using var response = await http.PostAsync(MethodUrl(method), content, cts.Token);
            var json = await response.Content.ReadAsStringAsync(cts.Token);

            TgResponse? parsed;
            try { parsed = JsonSerializer.Deserialize<TgResponse>(json, Json); }
            catch (JsonException) { parsed = null; }
            if (parsed is null)
                throw new TelegramApiException((int)response.StatusCode, $"Unreadable response from {method}.");

            if (parsed.Ok)
                return parsed.Result.Deserialize<T>(Json)!;

            var retryAfter = parsed.Parameters?.RetryAfter;
            if (parsed.ErrorCode == 429 && attempt < MaxRetries)
            {
                var wait = TimeSpan.FromSeconds(Math.Max(1, retryAfter ?? 3));
                if (wait > MaxRetryWait) wait = MaxRetryWait;
                logger.LogWarning("Telegram flood control on {Method}: waiting {Seconds}s.", method, wait.TotalSeconds);
                await Task.Delay(wait, ct);
                continue;
            }
            throw new TelegramApiException(parsed.ErrorCode, parsed.Description ?? "Unknown error",
                retryAfter, parsed.Parameters?.MigrateToChatId);
        }
    }

    // ── Bot ───────────────────────────────────────────────────────────────────

    public Task<TgUser> GetMeAsync(CancellationToken ct = default) => CallAsync<TgUser>("getMe", null, ct);

    public Task<bool> SetWebhookAsync(string url, string secret, IReadOnlyList<string> allowedUpdates, CancellationToken ct = default) =>
        CallAsync<bool>("setWebhook", new { url, secret_token = secret, allowed_updates = allowedUpdates }, ct);

    public Task<bool> DeleteWebhookAsync(CancellationToken ct = default) =>
        CallAsync<bool>("deleteWebhook", new { drop_pending_updates = false }, ct);

    public Task<List<TgUpdate>> GetUpdatesAsync(long offset, int timeoutSeconds, IReadOnlyList<string> allowedUpdates, CancellationToken ct) =>
        CallAsync<List<TgUpdate>>("getUpdates",
            new { offset, timeout = timeoutSeconds, allowed_updates = allowedUpdates }, ct,
            TimeSpan.FromSeconds(timeoutSeconds + 15));

    public Task<bool> SetMyCommandsAsync(IEnumerable<(string Command, string Description)> commands, CancellationToken ct = default) =>
        CallAsync<bool>("setMyCommands",
            new { commands = commands.Select(c => new { command = c.Command, description = c.Description }).ToList() }, ct);

    // ── Messages ──────────────────────────────────────────────────────────────

    private static object? Keyboard(IReadOnlyList<IReadOnlyList<TgButton>>? rows) =>
        rows is null ? null : new { inline_keyboard = rows };

    private static object? ReplyTo(int? messageId) =>
        messageId is int id and > 0 ? new { message_id = id, allow_sending_without_reply = true } : null;

    /// <summary>Sends an HTML message. <paramref name="threadId"/> is the forum topic (null = none).</summary>
    public Task<TgMessage> SendMessageAsync(
        long chatId, int? threadId, string html, IReadOnlyList<IReadOnlyList<TgButton>>? keyboard = null,
        int? replyTo = null, bool silent = false, CancellationToken ct = default) =>
        CallAsync<TgMessage>("sendMessage", new
        {
            chat_id = chatId,
            message_thread_id = threadId,
            text = html,
            parse_mode = "HTML",
            link_preview_options = new { is_disabled = true },
            reply_markup = Keyboard(keyboard),
            reply_parameters = ReplyTo(replyTo),
            disable_notification = silent ? true : (bool?)null,
        }, ct);

    /// <summary>Replaces a message's text and keyboard (an empty keyboard removes it).</summary>
    public Task<JsonElement> EditMessageTextAsync(
        long chatId, int messageId, string html, IReadOnlyList<IReadOnlyList<TgButton>>? keyboard = null, CancellationToken ct = default) =>
        CallAsync<JsonElement>("editMessageText", new
        {
            chat_id = chatId,
            message_id = messageId,
            text = html,
            parse_mode = "HTML",
            link_preview_options = new { is_disabled = true },
            reply_markup = Keyboard(keyboard ?? []),
        }, ct);

    public Task<bool> DeleteMessageAsync(long chatId, int messageId, CancellationToken ct = default) =>
        CallAsync<bool>("deleteMessage", new { chat_id = chatId, message_id = messageId }, ct);

    public Task<bool> PinChatMessageAsync(long chatId, int messageId, CancellationToken ct = default) =>
        CallAsync<bool>("pinChatMessage", new { chat_id = chatId, message_id = messageId, disable_notification = true }, ct);

    public Task<bool> UnpinChatMessageAsync(long chatId, int messageId, CancellationToken ct = default) =>
        CallAsync<bool>("unpinChatMessage", new { chat_id = chatId, message_id = messageId }, ct);

    public Task<bool> AnswerCallbackQueryAsync(string id, string? text = null, bool showAlert = false, CancellationToken ct = default) =>
        CallAsync<bool>("answerCallbackQuery", new { callback_query_id = id, text, show_alert = showAlert ? true : (bool?)null }, ct);

    /// <summary>A quick emoji reaction to acknowledge a message (👀 when a line has gone to the GM).</summary>
    public Task<bool> SetReactionAsync(long chatId, int messageId, string emoji, CancellationToken ct = default) =>
        CallAsync<bool>("setMessageReaction", new
        {
            chat_id = chatId,
            message_id = messageId,
            reaction = new[] { new { type = "emoji", emoji } },
        }, ct);

    /// <summary>"typing…" in the chat for about five seconds (the GM is working on its turn).</summary>
    public Task<bool> SendChatActionAsync(long chatId, int? threadId, string action = "typing", CancellationToken ct = default) =>
        CallAsync<bool>("sendChatAction", new { chat_id = chatId, message_thread_id = threadId, action }, ct);

    // ── Chats ─────────────────────────────────────────────────────────────────

    public Task<TgChatMember> GetChatMemberAsync(long chatId, long userId, CancellationToken ct = default) =>
        CallAsync<TgChatMember>("getChatMember", new { chat_id = chatId, user_id = userId }, ct);
}
