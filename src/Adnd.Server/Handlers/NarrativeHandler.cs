using Adnd.Server.Data;
using Adnd.Server.Events;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Handlers;

public class NarrativeHandler(AppDbContext db, IEventBus eventBus)
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

        if (msg.SessionId != Guid.Empty)
        {
            db.Messages.Add(new Message
            {
                SessionId = msg.SessionId,
                Content = msg.NarrativeText,
                Type = "GM",
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        await db.SaveChangesAsync();
        await eventBus.PublishAsync(new GameNarrationStarted(msg.GameId, msg.AgentCallId));
    }
}
