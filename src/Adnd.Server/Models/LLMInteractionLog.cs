using System.Text.Json;

namespace Adnd.Server.Models;

/// <summary>
/// Logs every LLM provider interaction with token statistics and origin tracking.
/// Provides visibility into all LLM usage across the system.
/// </summary>
public class LLMInteractionLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid? PresetId { get; set; }
    public LLMPreset? Preset { get; set; }

    // Provider info
    public string ProviderType { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;

    // Token statistics
    public int? PromptTokens { get; set; }
    public int? CompletionTokens { get; set; }
    public int? TotalTokens { get; set; }

    // Timing
    public int DurationMs { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;

    // Status
    public bool Success { get; set; } = true;
    public string? Error { get; set; }

    // Content (truncated for storage)
    public string? SystemPrompt { get; set; }
    public string? UserPrompt { get; set; }
    public string? Response { get; set; }

    // Request/Response metadata (truncated)
    public string? RequestJson { get; set; }
    public string? ResponseJson { get; set; }

    // Origin tracking
    public string Origin { get; set; } = string.Empty; // "game", "session", "agent", "manual"
    public Guid? OriginGameId { get; set; }
    public Guid? OriginSessionId { get; set; }
    public string? OriginAgent { get; set; } // Agent type name
    public string? OriginAction { get; set; } // Agent action
    public string? EndpointUrl { get; set; } // API endpoint URL used

    // Navigation
    public User? User { get; set; }
}
