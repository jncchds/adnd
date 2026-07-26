using System.Text.Json;

namespace Adnd.Server.Models;

public class ToolCallCoordinator
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AgentCallId { get; set; }
    public int TotalTools { get; set; }
    public int CompletedTools { get; set; }
    public int CurrentToolIndex { get; set; }
    public JsonElement ToolResults { get; set; }

    public AgentCall AgentCall { get; set; } = null!;
}
