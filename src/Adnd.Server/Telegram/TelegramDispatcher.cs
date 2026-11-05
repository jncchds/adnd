using System.Collections.Concurrent;
using Adnd.Server.Data;
using Adnd.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Telegram;

/// <summary>
/// Sends everything the bot posts on its own. Reads game events from <see cref="TelegramEventSink"/> and hands them
/// to one <see cref="ChatWorker"/> per chat — each bound group or topic, and the private chat of each linked
/// account — which paces its sends to Telegram's per-chat limits, so a slow chat only delays itself and nothing here
/// can slow a GM turn.
/// </summary>
public sealed class TelegramDispatcher(
    TelegramOptions options,
    TelegramEventSink sink,
    TelegramBotState bot,
    TelegramApi api,
    IServiceScopeFactory scopes,
    ILogger<TelegramDispatcher> logger) : BackgroundService
{
    private readonly ConcurrentDictionary<(long ChatId, int Thread), ChatWorker> _workers = new();
    private CancellationToken _stopping;

    internal TelegramApi Api => api;
    internal TelegramBotState Bot => bot;
    internal IServiceScopeFactory Scopes => scopes;
    internal ILogger Logger => logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled) return;
        _stopping = stoppingToken;
        try
        {
            await bot.WaitReadyAsync(stoppingToken);

            using (var scope = scopes.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                // A deleted game's chats stay quiet (its bindings are left in place for the record).
                var bindings = await db.TelegramBindings.AsNoTracking()
                    .Where(b => b.RevokedAt == null && db.Games.Any(g => g.Id == b.GameId))
                    .ToListAsync(stoppingToken);
                foreach (var binding in bindings.Where(b => !b.IsPrivate))
                    StartWorker(new ChatWorker(this, binding.ChatId, binding.ThreadId, null, binding, isNew: false));

                var linked = await db.Users.AsNoTracking().Where(u => u.TelegramUserId != null)
                    .Select(u => new { u.Id, ChatId = u.TelegramUserId!.Value }).ToListAsync(stoppingToken);
                foreach (var user in linked)
                {
                    var followed = bindings.FirstOrDefault(b => b.IsPrivate && b.UserId == user.Id && b.ChatId == user.ChatId);
                    StartWorker(new ChatWorker(this, user.ChatId, null, user.Id, followed, isNew: false));
                }
            }
            logger.LogInformation("Telegram: {Count} chat(s) loaded.", _workers.Count);

            await foreach (var ev in sink.Reader.ReadAllAsync(stoppingToken))
                foreach (var worker in _workers.Values)
                    if (worker.Wants(ev.GameId)) worker.Post(ev);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    /// <summary>A group or topic was just bound: start its worker, which greets the chat (unless <paramref name="welcome"/> is off).</summary>
    public void AttachGroup(TelegramBinding binding, bool welcome = true)
    {
        Detach(binding.ChatId, binding.ThreadId);
        StartWorker(new ChatWorker(this, binding.ChatId, binding.ThreadId, null, binding, isNew: welcome));
    }

    /// <summary>
    /// The private chat of a linked user: started if it is not running (after linking, or after Telegram refused a
    /// delivery and the user wrote again), and told to re-read which game it follows.
    /// </summary>
    public void EnsurePrivate(Guid userId, long chatId)
    {
        if (_workers.TryGetValue((chatId, 0), out var worker) && worker.UserId == userId)
        {
            worker.Enqueue(w => w.ReloadAsync());
            return;
        }
        Detach(chatId, null);
        StartWorker(new ChatWorker(this, chatId, null, userId, null, isNew: false, reloadFirst: true));
    }

    /// <summary>Stops the chat's worker (unbound, unlinked).</summary>
    public void Detach(long chatId, int? threadId)
    {
        if (_workers.TryRemove((chatId, threadId ?? 0), out var worker)) worker.Stop();
    }

    public ChatWorker? Worker(long chatId, int? threadId) => _workers.GetValueOrDefault((chatId, threadId ?? 0));

    /// <summary>Runs <paramref name="job"/> on the chat's worker, in order with its other sends.</summary>
    public bool Enqueue(long chatId, int? threadId, Func<ChatWorker, Task> job)
    {
        if (!_workers.TryGetValue((chatId, threadId ?? 0), out var worker)) return false;
        worker.Enqueue(job);
        return true;
    }

    private void StartWorker(ChatWorker worker)
    {
        if (_workers.TryAdd((worker.ChatId, worker.ThreadId ?? 0), worker))
            worker.Start(_stopping);
    }

    internal void OnWorkerEnded(ChatWorker worker)
    {
        var key = (worker.ChatId, worker.ThreadId ?? 0);
        if (_workers.TryGetValue(key, out var current) && ReferenceEquals(current, worker))
            _workers.TryRemove(key, out _);
    }
}
