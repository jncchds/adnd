using System.Text.Json;

namespace Adnd.Server.Models;

public class Whisper
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public Guid? FromPlayerId { get; set; }
    public WhisperType Type { get; set; }
    public string Content { get; set; } = string.Empty;
    public JsonElement TargetPlayerIds { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public GameSession Session { get; set; } = null!;
}
