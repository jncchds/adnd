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

    /// <summary>
    /// The tool calls the LLM requested, as a JSON array. Persisted here because
    /// AgentCall.Output holds the narrative text, not the tool payload — reading the
    /// tool list from there meant every call past the first was silently dropped.
    /// </summary>
    public JsonElement ToolCalls { get; set; }

    public AgentCall AgentCall { get; set; } = null!;
}
