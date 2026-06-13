using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace Adnd.Server.Models;

public class ToolCallCoordinator
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SagaId { get; set; }
    public Guid GameId { get; set; }
    public int TotalTools { get; set; }
    public int CurrentIndex { get; set; }
    [NotMapped]
    public List<ToolCallResult> CompletedTools { get; set; } = new();
    public string? ToolsJson { get; set; }  // JSON: [{name, arguments}, ...]
    public CoordinatorStatus Status { get; set; } = CoordinatorStatus.Active;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}

public enum CoordinatorStatus
{
    Active = 0,
    Completed = 1,
    Abandoned = 2
}

public class ToolCallResult
{
    public int Index { get; set; }
    public string ToolName { get; set; } = "";
    public string Result { get; set; } = "";
    public string? Error { get; set; }
}

public class ToolCallInfo
{
    public string Name { get; set; } = "";
    public string Arguments { get; set; } = "";
}
