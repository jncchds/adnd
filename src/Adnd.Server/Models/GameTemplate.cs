namespace Adnd.Server.Models;

/// <summary>
/// A saved game configuration that can be reused when creating new games.
/// Templates store the game name, system, LLM preset, language, plot seed, and parameters.
/// </summary>
public class GameTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Template name (displayed in the UI).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Owner of this template.</summary>
    public Guid UserId { get; set; }
    public User? User { get; set; }

    /// <summary>Whether other users can see this template (future multi-user feature).</summary>
    public bool IsPublic { get; set; } = false;

    /// <summary>Default game name prefix (e.g., "My New Adventure").</summary>
    public string? DefaultName { get; set; }

    /// <summary>RPG system (e.g., "dnd5e", "pf2e", "coc7e").</summary>
    public string SystemId { get; set; } = "dnd5e";

    /// <summary>LLM preset ID for the AI-GM.</summary>
    public Guid? LLMPresetId { get; set; }
    public LLMPreset? LLMPreset { get; set; }

    /// <summary>LLM preset name stored at save time (for display when preset is deleted).</summary>
    public string? LLMPresetName { get; set; }

    /// <summary>Narration language (e.g., "English", "Spanish").</summary>
    public string Language { get; set; } = "English";

    /// <summary>Initial plot seed / story premise.</summary>
    public string? PlotSeed { get; set; }

    /// <summary>Game parameters (tone, difficulty, pacing).</summary>
    public string? GameParameters { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
