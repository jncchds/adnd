using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Adnd.Server.Events;
using MediatR;
using System.Text.Json;

namespace Adnd.Server.Hubs;

public partial class GameHub
{
    // ==================== Helper Methods ====================

    private ToolCallConfirmationResponse BuildToolCallConfirmationResponse(GMToolCall toolCall, bool approved)
    {
        return new ToolCallConfirmationResponse
        {
            Id = toolCall.Id,
            ToolName = toolCall.ToolName,
            Approved = approved,
            OutputMessage = toolCall.OutputMessage,
            Status = toolCall.Status
        };
    }

}
