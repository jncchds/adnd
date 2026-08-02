namespace Adnd.Server.Dtos;

// Request shapes for the QuickWins endpoints. These previously bound the EF entities
// directly, letting the caller pick Id, GameId, UserId and IsDefault.

public record CreateSessionNoteDto(Guid GameId, Guid? SessionId, string Title, string Content);
public record UpdateSessionNoteDto(string? Title, string? Content);

public record CreatePromptTemplateDto(Guid? GameId, string Name, string Type, string Content);
public record UpdatePromptTemplateDto(string? Name, string? Type, string? Content);

public record CreateGameTemplateDto(
    string Name,
    string? Description,
    string SystemId,
    string Language,
    string? PlotSeed,
    string? GameParameters,
    Guid? LLMPresetId);

public record UpdateGameTemplateDto(
    string? Name,
    string? Description,
    string? SystemId,
    string? Language,
    string? PlotSeed,
    string? GameParameters,
    Guid? LLMPresetId);
