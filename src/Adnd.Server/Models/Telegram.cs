namespace Adnd.Server.Models;

/// <summary>
/// A Telegram chat (or one forum topic of it) following a game. A group or topic is the table: everyone there sees
/// what the whole table sees, and a linked player's plain message is an in-character line. A private chat follows a
/// game for one linked user (<see cref="UserId"/>) and also shows what only they may see.
/// </summary>
public class TelegramBinding
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GameId { get; set; }
    public long ChatId { get; set; }
    /// <summary>The forum topic, or null for the whole chat (or a forum's General topic).</summary>
    public int? ThreadId { get; set; }
    /// <summary>Telegram's chat type: private, group, supergroup (channels cannot play).</summary>
    public string ChatType { get; set; } = "group";
    public string ChatTitle { get; set; } = string.Empty;
    /// <summary>A private chat's user — whose whispers, prompts and secret rolls it shows. Null for groups.</summary>
    public Guid? UserId { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RevokedAt { get; set; }
    public string? RevokedReason { get; set; }

    public bool IsActive => RevokedAt is null;
    public bool IsPrivate => ChatType == "private";

    public Game Game { get; set; } = null!;
}

public enum TelegramCodeKind { Account, Bind }

/// <summary>
/// A one-time code handed to the bot: <see cref="TelegramCodeKind.Account"/> links the sender's Telegram account to
/// <see cref="UserId"/>; <see cref="TelegramCodeKind.Bind"/> makes the chat it is sent in follow <see cref="GameId"/>.
/// Only the SHA-256 of the code is stored.
/// </summary>
public class TelegramCode
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string CodeHash { get; set; } = string.Empty;
    public TelegramCodeKind Kind { get; set; }
    public Guid UserId { get; set; }
    public Guid? GameId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
}

public enum TelegramLinkKind { Message, CombatCard }

/// <summary>
/// What the bot sent to a chat for a game row — a message, or a fight's combat card — so a change edits it in place,
/// a removal deletes it, and nothing is sent twice. A line a player typed in that chat is linked to their own
/// Telegram message (<see cref="Own"/>) and never echoed back.
/// </summary>
public class TelegramMessageLink
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public long ChatId { get; set; }
    /// <summary>The forum topic; 0 for none (part of the unique key, so not nullable).</summary>
    public int ThreadId { get; set; }
    public TelegramLinkKind Kind { get; set; }
    public Guid RefId { get; set; }
    public int TelegramMessageId { get; set; }
    public string? ContentHash { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public const string Own = "own";
}
