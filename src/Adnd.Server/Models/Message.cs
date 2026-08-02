using System.Text.Json;
using Pgvector;

namespace Adnd.Server.Models;

public class Message : ISoftDelete
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public Guid? PlayerId { get; set; }

    public string Content { get; set; } = string.Empty;
    public string Type { get; set; } = "Chat";
    public JsonElement Metadata { get; set; }
    public bool IsOOC { get; set; }

    public Guid? WhisperFromId { get; set; }
    public Guid? WhisperToId { get; set; }
    public string? WhisperTarget { get; set; }

    /// <summary>
    /// A roll only the roller and the GM may see. Deliberately separate from the whisper
    /// fields: a whisper is hidden from the AI narrator too, whereas a secret roll must
    /// still reach it — the GM is precisely who a secret roll is secret *for*. See
    /// <c>MessageVisibility</c>.
    /// </summary>
    public bool IsSecret { get; set; }

    public Vector? Embedding { get; set; }

    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public GameSession Session { get; set; } = null!;
    public Player? Player { get; set; }
}
