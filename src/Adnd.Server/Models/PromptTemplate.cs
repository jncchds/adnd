namespace Adnd.Server.Models;

/// <summary>
/// Configurable prompt templates per game/session. Allows GMs to customize
/// system prompts for different LLM operations (narration, consistency, etc.).
/// </summary>
public class PromptTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GameId { get; set; }
    public Game? Game { get; set; }
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // "narration", "consistency", "review", "session_summary", "custom"
    public string Prompt { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool IsDefault { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
