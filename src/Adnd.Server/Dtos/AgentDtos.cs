namespace Adnd.Server.Dtos;

/// <param name="Pending">Set on a narrator turn whose prompts are still to be built:
/// <c>UserPrompt</c> then holds only what the player said, and <c>AgentSaga.Start</c> fills
/// in the rest when the turn actually begins.</param>
public record GMDispatchOptions(string SystemPrompt, string UserPrompt, bool Stream = false, int? MaxTokens = null, bool IncludeTools = true,
    string? Pending = null, Guid? SpeakerPlayerId = null)
{
    public const string PendingTurn = "turn";
    public const string PendingOpening = "opening";
}
