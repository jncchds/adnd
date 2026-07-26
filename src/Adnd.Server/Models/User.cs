namespace Adnd.Server.Models;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastLoginAt { get; set; }

    public ICollection<Game> Games { get; set; } = [];
    public ICollection<LLMPreset> LLMPresets { get; set; } = [];
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
}
