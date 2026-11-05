namespace Adnd.Server.Hubs;

public partial class GameHub
{
    public Task TriggerNarrate(Guid gameId, string prompt)
        => Chat(() => chat.NarrateAsync(gameId, CurrentUserId, prompt));

    public Task TriggerSuggest(Guid gameId, string prompt)
        => Chat(() => chat.AskGmAsync(gameId, CurrentUserId, prompt));
}
