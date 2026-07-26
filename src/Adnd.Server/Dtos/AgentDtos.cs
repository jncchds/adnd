namespace Adnd.Server.Dtos;

public record GMDispatchOptions(string SystemPrompt, string UserPrompt, bool Stream = false, int? MaxTokens = null, bool IncludeTools = true);
