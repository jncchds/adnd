using System.Text.Json.Serialization;

namespace Adnd.Server.Models;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;

    // Never serialize. GET /api/games/{id}/players returns Player entities with User
    // included, which used to hand every game member's BCrypt hash to the client.
    [JsonIgnore]
    public string PasswordHash { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastLoginAt { get; set; }

    /// <summary>The linked Telegram account (one each way), set by sending the bot a code made in User Settings.</summary>
    public long? TelegramUserId { get; set; }
    public string? TelegramUsername { get; set; }
    public DateTimeOffset? TelegramLinkedAt { get; set; }

    public ICollection<Game> Games { get; set; } = [];
    public ICollection<LLMPreset> LLMPresets { get; set; } = [];
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
}
