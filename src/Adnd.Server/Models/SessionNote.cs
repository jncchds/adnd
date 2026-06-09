namespace Adnd.Server.Models;

/// <summary>
/// GM-only session notes. Visible only to GM/Creator roles.
/// Stored per session for quick reference during play.
/// </summary>
public class SessionNote
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public GameSession? Session { get; set; }
    public Guid CreatorId { get; set; }
    public User? Creator { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
