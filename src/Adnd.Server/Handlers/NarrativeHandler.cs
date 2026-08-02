using Adnd.Server.Data;
using Adnd.Server.Events;
using Adnd.Server.Hubs;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Handlers;

public class NarrativeHandler(AppDbContext db, IEventBus eventBus, IHubContext<GameHub> hub)
{
    public async Task HandleAsync(NarrativeReady msg)
    {
        var call = await db.AgentCalls.FindAsync(msg.AgentCallId);
        if (call != null)
        {
            call.Status = AgentCallStatus.Completed;
            call.CurrentStep = (int)SagaStep.Completed;
            call.OutputMessage = msg.NarrativeText;
        }

        Message? saved = null;
        if (msg.SessionId != Guid.Empty)
        {
            saved = new Message
            {
                SessionId = msg.SessionId,
                Content = msg.NarrativeText,
                Type = "GM",
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.Messages.Add(saved);
        }

        await db.SaveChangesAsync();
        await eventBus.PublishAsync(new GameNarrationStarted(msg.GameId, msg.AgentCallId));

        if (saved != null)
        {
            var dto = new MessageDto(saved.Id, saved.SessionId, null, saved.Content, saved.Type, false, saved.CreatedAt, null);
            await hub.Clients.Group(msg.GameId.ToString()).SendAsync("NewMessage", dto);
        }
    }
}
