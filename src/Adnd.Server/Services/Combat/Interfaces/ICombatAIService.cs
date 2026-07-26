namespace Adnd.Server.Services.Combat;

public interface ICombatAIService
{
    Task<List<AICombatSuggestion>> GetSuggestionsAsync(Guid combatId, Guid gameId, CancellationToken ct = default);
}
