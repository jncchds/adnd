using System.Threading.Channels;

namespace Adnd.Server.Telegram;

/// <summary>A game event as the bot sees it. Each chat worker reads what it needs back from the database.</summary>
public abstract record TgGameEvent(Guid GameId);
/// <summary>A message was posted, changed (a roll prompt answered) or removed.</summary>
public sealed record TgMessageEvent(Guid GameId, Guid MessageId) : TgGameEvent(GameId);
/// <summary>The fight changed: the combat card is redrawn.</summary>
public sealed record TgCombatEvent(Guid GameId) : TgGameEvent(GameId);
/// <summary>The GM's turn moved on a step (<c>SagaStep</c> names; Completed / Failed end it).</summary>
public sealed record TgActivityEvent(Guid GameId, string Step) : TgGameEvent(GameId);
/// <summary>A GM turn failed for good.</summary>
public sealed record TgErrorEvent(Guid GameId, string Message) : TgGameEvent(GameId);

/// <summary>
/// Where <c>GameNotifier</c> drops game events for the bot. Posting never blocks or throws, so a slow or unreachable
/// Telegram can never slow a GM turn down; <see cref="TelegramDispatcher"/> reads the events. With the bot off,
/// nothing is queued.
/// </summary>
public sealed class TelegramEventSink(TelegramOptions options)
{
    private readonly Channel<TgGameEvent> _events = Channel.CreateUnbounded<TgGameEvent>(
        new UnboundedChannelOptions { SingleReader = true });

    public bool Enabled { get; } = options.Enabled;
    public ChannelReader<TgGameEvent> Reader => _events.Reader;

    public void Post(TgGameEvent ev)
    {
        if (Enabled) _events.Writer.TryWrite(ev);
    }
}
