using System.Text.Json;
using Adnd.Server.Data;
using Adnd.Server.Models;
using Microsoft.Extensions.Logging;

namespace Adnd.Server.Services;

public interface IGMToolCallService
{
    Task<GMToolCall> RecordAndExecuteAsync(Guid gameId, Guid sessionId, string toolName, JsonElement arguments, CancellationToken ct);
}

public class GMToolCallService(AppDbContext db, IGMToolRegistry toolRegistry, ILogger<GMToolCallService> logger) : IGMToolCallService
{
    public async Task<GMToolCall> RecordAndExecuteAsync(Guid gameId, Guid sessionId, string toolName, JsonElement arguments, CancellationToken ct)
    {
        var toolCall = new GMToolCall
        {
            GameId = gameId,
            SessionId = sessionId,
            ToolName = toolName,
            Arguments = arguments,
            Status = GMToolCallStatus.Running,
            StartedAt = DateTimeOffset.UtcNow
        };
        db.GMToolCalls.Add(toolCall);
        await db.SaveChangesAsync(ct);

        string resultText;
        try
        {
            resultText = await toolRegistry.ExecuteToolAsync(toolName, arguments, gameId, sessionId, ct);
            toolCall.Status = GMToolCallStatus.Completed;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GM tool '{ToolName}' failed for game {GameId}", toolName, gameId);
            resultText = $"Error: {ex.Message}";
            toolCall.Status = GMToolCallStatus.Failed;
        }

        toolCall.Result = JsonSerializer.SerializeToElement(new { result = resultText });
        toolCall.CompletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        return toolCall;
    }
}
