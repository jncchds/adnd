using Microsoft.AspNetCore.Mvc;
using Adnd.Server.Models;
using System.Text.Json;

namespace Adnd.Server.Controllers;

public partial class AdminController
{
    // ==================== Helpers ====================

    private static int GetProficiencyBonus(int level)
    {
        if (level <= 4) return 2;
        if (level <= 8) return 3;
        if (level <= 12) return 4;
        if (level <= 16) return 5;
        return 6;
    }
}

// Request DTOs
public class CreateNPCRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public JsonElement? Attributes { get; set; }
    public JsonElement? Skills { get; set; }
    public JsonElement? Inventory { get; set; }
    public JsonElement? Spells { get; set; }
    public Guid? PlotThreadId { get; set; }
}

public class UpdateNPCRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public JsonElement? Attributes { get; set; }
    public JsonElement? Skills { get; set; }
    public JsonElement? Inventory { get; set; }
    public JsonElement? Spells { get; set; }
    public Guid? PlotThreadId { get; set; }
}

public class CreatePlotThreadRequest
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class UpdatePlotThreadRequest
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public int? Status { get; set; }
    public List<Guid>? KeyEventMessageIds { get; set; }
}

public class AddKeyEventRequest
{
    public Guid MessageId { get; set; }
}

public class UpdateCharacterRequest
{
    public string? Name { get; set; }
    public string? Class { get; set; }
    public int? Level { get; set; }
    public int? CurrentHP { get; set; }
    public int? MaxHP { get; set; }
    public JsonElement? Attributes { get; set; }
    public JsonElement? Skills { get; set; }
    public JsonElement? Inventory { get; set; }
    public JsonElement? Spells { get; set; }
    public JsonElement? Conditions { get; set; }
}

public class CreateSystemRequest
{
    public string Name { get; set; } = string.Empty;
    public string JsonDefinition { get; set; } = string.Empty;
}

public class SimilarThreadRequest
{
    public string Query { get; set; } = string.Empty;
    public int Limit { get; set; } = 5;
}

// ============= Whisper DTOs =============

public class SendWhisperRequest
{
    public Guid SessionId { get; set; }
    public string Targets { get; set; } = string.Empty; // "player:{id}", "all", "group:{name}"
    public Adnd.Server.Models.WhisperType Type { get; set; }
    public string Content { get; set; } = string.Empty;
}

public class SendGMWhisperRequest
{
    public List<Guid> TargetPlayerIds { get; set; } = new();
    public Adnd.Server.Models.WhisperType Type { get; set; }
    public string Content { get; set; } = string.Empty;
}

// ============= Agent DTOs =============

public class CreateAgentCallRequest
{
    public Guid? SessionId { get; set; }
    public AgentType FromAgent { get; set; }
    public AgentType ToAgent { get; set; }
    public AgentAction Action { get; set; }
    public string? Input { get; set; }
}

// ============= Game State DTOs =============

public class UpdateGameStateRequest
{
    public string? GameState { get; set; }
    public string? PlotSeed { get; set; }
    public string? GameParameters { get; set; }
}

// ============= PlotWeaver DTOs =============

public class AdjustMomentumRequest
{
    public float Delta { get; set; }
    public string Reason { get; set; } = string.Empty;
}

// ============= Sway DTO =============

public class SwayRequest
{
    public string Direction { get; set; } = string.Empty;
}

// ============= Trigger DTO =============

public class TriggerRequest
{
    public AgentAction Action { get; set; }
    public string? Input { get; set; }
}
