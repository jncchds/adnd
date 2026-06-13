# Reactive Saga Architecture Implementation Plan

> **REQUIRED SUB-SKILL:** Use the executing-plans skill to implement this plan task-by-task.

**Goal:** Replace the GameAgent polling loop with fully reactive RabbitMQ saga choreography using small composable handlers.

**Architecture:** Each game agent call becomes a saga — a chain of small handlers connected by typed events published to RabbitMQ. The GameAgent becomes a RabbitMQ consumer (prefetch=1) instead of a loop. Multi-tool workflows use a persisted `ToolCallCoordinator` that survives crashes.

**Tech Stack:** ASP.NET Core 10, RabbitMQ.Client, EF Core, PostgreSQL, MediatR (existing)

---

## Phase 1: Foundation — Types, Models, Interfaces

### Task 1: Add new event types to GameEvents.cs

**Files:**
- Modify: `src/Adnd.Server/Events/GameEvents.cs`

**Step 1: Add saga lifecycle events**

Append these events to `GameEvents.cs` (before the helper enums):

```csharp
// ==================== Saga Events ====================

public record AgentCallQueued(Guid SagaId, Guid GameId) : IGameEvent;

public record LLMDispatchRequested(
    Guid SagaId,
    string SystemPrompt,
    string UserPrompt,
    string? Options) : IGameEvent;

public record LLMResponseReceived(
    Guid SagaId,
    string Response,
    bool HasToolCalls,
    int ToolCallCount) : IGameEvent;

public record ToolCallRequested(
    Guid SagaId,
    int ToolIndex,
    string ToolName,
    string ToolArgs) : IGameEvent;

public record ToolCallCompleted(
    Guid SagaId,
    int ToolIndex,
    string Result,
    string? Error) : IGameEvent;

public record CoordinatorUpdated(
    Guid SagaId,
    int CurrentIndex,
    int TotalTools) : IGameEvent;

public record LLMFollowUpRequested(
    Guid SagaId,
    List<ToolResult> ToolResults) : IGameEvent;

public record ToolResult(int Index, string ToolName, string Result, string? Error);

public record NarrativeReady(
    Guid SagaId,
    string Narrative) : IGameEvent;

public record AgentCallCompleted(Guid SagaId) : IGameEvent;

public record AgentCallFailed(Guid SagaId, string Error) : IGameEvent;
```

**Step 2: Verify syntax**

Run: `cd src/Adnd.Server && dotnet build --no-restore 2>&1 | grep -i error`
Expected: no output (no errors)

**Step 3: Commit**

```bash
git add src/Adnd.Server/Events/GameEvents.cs
git commit -m "feat: add saga event types for reactive architecture"
```

---

### Task 2: Add SagaStep enum and coordinator model

**Files:**
- Modify: `src/Adnd.Server/Models/AgentCall.cs`
- Create: `src/Adnd.Server/Models/ToolCallCoordinator.cs`

**Step 1: Add SagaStep enum to AgentCall.cs**

Append after `AgentCallStatus` enum:

```csharp
public enum SagaStep
{
    None = 0,
    Init = 1,
    LLMDispatchRequested = 2,
    LLMResponseReceived = 3,
    ToolCallRequested = 4,
    ToolCallCompleted = 5,
    LLMFollowUpRequested = 6,
    NarrativeReady = 7,
    Completed = 8,
    Failed = 9
}
```

**Step 2: Add CurrentStep column to AgentCall entity**

In the `AgentCall` class, add after `DurationMs`:

```csharp
public SagaStep CurrentStep { get; set; } = SagaStep.Init;
```

**Step 3: Create ToolCallCoordinator.cs**

```csharp
namespace Adnd.Server.Models;

public class ToolCallCoordinator
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SagaId { get; set; }
    public Guid GameId { get; set; }
    public int TotalTools { get; set; }
    public int CurrentIndex { get; set; }
    public string? ToolName { get; set; }
    public string? ToolArgs { get; set; }
    public List<ToolCallResult> CompletedTools { get; set; } = new();
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
```

**Step 4: Add ToolCallCoordinator to AppDbContext**

In `AppDbContext.cs`, add after `public DbSet<AgentCall> AgentCalls`:

```csharp
public DbSet<ToolCallCoordinator> ToolCallCoordinators { get; set; }
```

**Step 5: Verify syntax**

Run: `cd src/Adnd.Server && dotnet build --no-restore 2>&1 | grep -i error`
Expected: no output

**Step 6: Commit**

```bash
git add src/Adnd.Server/Models/AgentCall.cs src/Adnd.Server/Models/ToolCallCoordinator.cs src/Adnd.Server/Data/AppDbContext.cs
git commit -m "feat: add SagaStep enum and ToolCallCoordinator model"
```

---

### Task 3: Add IEventHandler interface and HandlerRegistry

**Files:**
- Create: `src/Adnd.Server/Services/IEventHandler.cs`
- Create: `src/Adnd.Server/Services/HandlerRegistry.cs`

**Step 1: Create IEventHandler.cs**

```csharp
using Adnd.Server.Events;

namespace Adnd.Server.Services;

public interface IEventHandler<TEvent> where TEvent : IGameEvent
{
    Task HandleAsync(TEvent evt, CancellationToken ct);
}
```

**Step 2: Create HandlerRegistry.cs**

```csharp
using Adnd.Server.Handlers;
using Adnd.Server.Events;
using System.Reflection;

namespace Adnd.Server.Services;

public interface IHandlerRegistry
{
    IReadOnlyDictionary<string, List<Type>> Handlers { get; }
}

public class HandlerRegistry : IHandlerRegistry
{
    private readonly Dictionary<string, List<Type>> _handlers;

    public IReadOnlyDictionary<string, List<Type>> Handlers => _handlers;

    public HandlerRegistry()
    {
        _handlers = new();
        RegisterHandlers();
    }

    private void RegisterHandlers()
    {
        var assembly = typeof(GameLifecycleHandler).Assembly;
        var iEventHandlerType = typeof(IEventHandler<>);

        foreach (var type in assembly.GetTypes()
            .Where(t => t.Namespace == "Adnd.Server.Handlers"
                     && !t.IsAbstract && !t.IsInterface
                     && t.GetInterfaces().Any(i =>
                         i.IsGenericType &&
                         i.GetGenericTypeDefinition() == iEventHandlerType)))
        {
            var handlerInterfaces = type.GetInterfaces()
                .Where(i => i.IsGenericType &&
                           i.GetGenericTypeDefinition() == iEventHandlerType);

            foreach (var handlerInterface in handlerInterfaces)
            {
                var eventType = handlerInterface.GetGenericArguments()[0];
                var key = eventType.FullName!;

                if (!_handlers.TryGetValue(key, out var list))
                {
                    list = new List<Type>();
                    _handlers[key] = list;
                }
                list.Add(type);
            }
        }
    }
}
```

**Step 3: Register HandlerRegistry in Program.cs**

In `Program.cs`, add after `// Event Bus — RabbitMQ-backed durable pub/sub`:

```csharp
// Handler Registry — scans Adnd.Server.Handlers for IEventHandler<T>
builder.Services.AddSingleton<IHandlerRegistry, HandlerRegistry>();
```

**Step 4: Verify syntax**

Run: `cd src/Adnd.Server && dotnet build --no-restore 2>&1 | grep -i error`
Expected: no output

**Step 5: Commit**

```bash
git add src/Adnd.Server/Services/IEventHandler.cs src/Adnd.Server/Services/HandlerRegistry.cs src/Adnd.Server/Program.cs
git commit -m "feat: add IEventHandler interface and HandlerRegistry"
```

---

## Phase 2: Create Saga Handlers

### Task 4: Create SagaOrchestratorHandler

**Files:**
- Create: `src/Adnd.Server/Handlers/SagaOrchestratorHandler.cs`

**Step 1: Create the handler**

```csharp
using Adnd.Server.Data;
using Adnd.Server.Events;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Adnd.Server.Handlers;

public class SagaOrchestratorHandler : IEventHandler<AgentCallQueued>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SagaOrchestratorHandler> _logger;

    public SagaOrchestratorHandler(IServiceProvider serviceProvider, ILogger<SagaOrchestratorHandler> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task HandleAsync(AgentCallQueued evt, CancellationToken ct)
    {
        _logger.LogInformation("[SAGA] Orchestrating | SagaId={SagaId}", evt.SagaId);

        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var call = await context.AgentCalls
            .FirstOrDefaultAsync(c => c.Id == evt.SagaId && c.GameId == evt.GameId, ct);

        if (call == null)
        {
            _logger.LogWarning("[SAGA] CallNotFound | SagaId={SagaId}", evt.SagaId);
            return;
        }

        call.CurrentStep = SagaStep.Init;
        await context.SaveChangesAsync(ct);

        // Determine action type and emit the appropriate next event
        var nextEvent = call.Action switch
        {
            AgentAction.Narrate or AgentAction.Generate or AgentAction.OpenNarrative
                => (IGameEvent)new LLMDispatchRequested(
                    evt.SagaId,
                    "You are the Game Master for a TTRPG session.",
                    call.Input ?? "Continue the narrative.",
                    null),

            AgentAction.Nudge => new LLMDispatchRequested(
                evt.SagaId,
                "You are the Game Master for a TTRPG session. The creator has sent a narrative nudge.",
                call.Input ?? "Incorporate the direction.",
                null),

            AgentAction.Query => new LLMDispatchRequested(
                evt.SagaId,
                "You are a helpful TTRPG assistant.",
                call.Input ?? "Answer the question.",
                null),

            AgentAction.Suggest => new LLMDispatchRequested(
                evt.SagaId,
                "You are a creative TTRPG Game Master assistant.",
                call.Input ?? "Suggest plot continuations.",
                null),

            AgentAction.ManageState => new AgentCallCompleted(evt.SagaId),

            _ => new AgentCallFailed(evt.SagaId, $"Unknown action: {call.Action}")
        };

        // Publish next event via the event bus
        var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();
        await eventBus.PublishAsync(nextEvent, ct);

        call.CurrentStep = call.Action == AgentAction.ManageState ? SagaStep.Completed : SagaStep.LLMDispatchRequested;
        await context.SaveChangesAsync(ct);

        _logger.LogInformation("[SAGA] NextStep | SagaId={SagaId} | Step={Step}", evt.SagaId, call.CurrentStep);
    }
}
```

**Step 2: Verify syntax**

Run: `cd src/Adnd.Server && dotnet build --no-restore 2>&1 | grep -i error`
Expected: no output

**Step 3: Commit**

```bash
git add src/Adnd.Server/Handlers/SagaOrchestratorHandler.cs
git commit -m "feat: add SagaOrchestratorHandler for AgentCallQueued"
```

---

### Task 5: Create LLMDispatchHandler

**Files:**
- Create: `src/Adnd.Server/Handlers/LLMDispatchHandler.cs`

**Step 1: Create the handler**

```csharp
using Adnd.Server.Data;
using Adnd.Server.Events;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Adnd.Server.Handlers;

public class LLMDispatchHandler : IEventHandler<LLMDispatchRequested>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<LLMDispatchHandler> _logger;

    public LLMDispatchHandler(IServiceProvider serviceProvider, ILogger<LLMDispatchHandler> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task HandleAsync(LLMDispatchRequested evt, CancellationToken ct)
    {
        _logger.LogInformation("[LLM] Dispatching | SagaId={SagaId}", evt.SagaId);

        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var call = await context.AgentCalls
            .FirstOrDefaultAsync(c => c.Id == evt.SagaId, ct);

        if (call?.Game == null || call.Game.LLMPreset == null)
        {
            await PublishFailure(scope, evt.SagaId, "LLM preset not configured", context, ct);
            return;
        }

        var provider = GetProvider(call.Game.LLMPreset);
        if (provider == null)
        {
            await PublishFailure(scope, evt.SagaId, $"LLM provider '{call.Game.LLMPreset.ProviderType}' not available", context, ct);
            return;
        }

        string result;
        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            result = await provider.CompleteAsync(evt.SystemPrompt, evt.UserPrompt, evt.Options);
            sw.Stop();

            // Log interaction
            var interactionLogger = scope.ServiceProvider.GetRequiredService<ILLMInteractionLogger>();
            var tokenUsage = provider.GetTokenUsage(result);
            await interactionLogger.LogInteractionAsync(
                call.Game.CreatorId, call.Game.LLMPresetId, provider.ProviderId,
                evt.Options?.Model ?? call.Game.LLMPreset.BaseModel,
                tokenUsage?.promptTokens, tokenUsage?.completionTokens, tokenUsage?.totalTokens,
                (int)sw.ElapsedMilliseconds, evt.SystemPrompt, evt.UserPrompt, result,
                null, provider.EndpointUrl, "saga", call.GameId, call.SessionId,
                "LLMDispatchHandler", "dispatch",
                call.Game.LLMPreset.Name, call.Game.LLMPreset.EndpointUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[LLM] DispatchFailed | SagaId={SagaId}", evt.SagaId);
            await PublishFailure(scope, evt.SagaId, ex.Message, context, ct);
            return;
        }

        // Determine if response has tool calls
        var hasToolCalls = result.Contains("\"name\":") || result.Contains("tool_calls");
        var toolCallCount = hasToolCalls ? CountToolCalls(result) : 0;

        var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();
        await eventBus.PublishAsync(new LLMResponseReceived(
            evt.SagaId, result, hasToolCalls, toolCallCount), ct);

        call.CurrentStep = SagaStep.LLMResponseReceived;
        await context.SaveChangesAsync(ct);

        _logger.LogInformation("[LLM] Dispatched | SagaId={SagaId} | HasToolCalls={HasTools} | Count={Count}",
            evt.SagaId, hasToolCalls, toolCallCount);
    }

    private ILLMProvider? GetProvider(LLMPreset preset)
    {
        var factory = _serviceProvider.GetRequiredService<ILLMProviderFactory>();
        return factory.CreateFromPreset(preset);
    }

    private int CountToolCalls(string response)
    {
        // Simple heuristic: count occurrences of tool call patterns
        var count = 0;
        var pos = 0;
        while ((pos = response.IndexOf("\"name\":", pos, StringComparison.Ordinal)) >= 0)
        {
            count++;
            pos += 7;
        }
        return count;
    }

    private async Task PublishFailure(IServiceScope scope, Guid sagaId, string error, AppDbContext context, CancellationToken ct)
    {
        var call = await context.AgentCalls.FirstOrDefaultAsync(c => c.Id == sagaId, ct);
        if (call != null)
        {
            call.Status = AgentCallStatus.Failed;
            call.Error = error;
            call.CompletedAt = DateTime.UtcNow;
            call.CurrentStep = SagaStep.Failed;
            await context.SaveChangesAsync(ct);
        }

        var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();
        await eventBus.PublishAsync(new AgentCallFailed(sagaId, error), ct);
    }
}
```

**Step 2: Verify syntax**

Run: `cd src/Adnd.Server && dotnet build --no-restore 2>&1 | grep -i error`
Expected: no output

**Step 3: Commit**

```bash
git add src/Adnd.Server/Handlers/LLMDispatchHandler.cs
git commit -m "feat: add LLMDispatchHandler for LLMDispatchRequested"
```

---

### Task 6: Create LLMResponseHandler

**Files:**
- Create: `src/Adnd.Server/Handlers/LLMResponseHandler.cs`

**Step 1: Create the handler**

```csharp
using Adnd.Server.Data;
using Adnd.Server.Events;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Adnd.Server.Handlers;

public class LLMResponseHandler : IEventHandler<LLMResponseReceived>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<LLMResponseHandler> _logger;

    public LLMResponseHandler(IServiceProvider serviceProvider, ILogger<LLMResponseHandler> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task HandleAsync(LLMResponseReceived evt, CancellationToken ct)
    {
        _logger.LogInformation("[SAGA] LLMResponse | SagaId={SagaId} | HasTools={HasTools}", evt.SagaId, evt.HasToolCalls);

        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var call = await context.AgentCalls.FirstOrDefaultAsync(c => c.Id == evt.SagaId, ct);
        if (call == null)
        {
            _logger.LogWarning("[SAGA] CallNotFound | SagaId={SagaId}", evt.SagaId);
            return;
        }

        var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();

        if (!evt.HasToolCalls)
        {
            // No tools — emit narrative directly
            call.CurrentStep = SagaStep.NarrativeReady;
            await context.SaveChangesAsync(ct);

            await eventBus.PublishAsync(new NarrativeReady(evt.SagaId, evt.Response), ct);
            return;
        }

        // Has tool calls — check if we already have a coordinator
        var coordinator = await context.ToolCallCoordinators
            .FirstOrDefaultAsync(c => c.SagaId == evt.SagaId && c.Status == CoordinatorStatus.Active, ct);

        if (coordinator == null)
        {
            // Create new coordinator
            coordinator = new ToolCallCoordinator
            {
                SagaId = evt.SagaId,
                GameId = call.GameId,
                TotalTools = evt.ToolCallCount,
                CurrentIndex = 0,
                Status = CoordinatorStatus.Active
            };
            context.ToolCallCoordinators.Add(coordinator);
            await context.SaveChangesAsync(ct);

            _logger.LogInformation("[SAGA] CoordinatorCreated | SagaId={SagaId} | Total={Total}", evt.SagaId, evt.ToolCallCount);
        }

        // Emit first tool call
        var toolName = ExtractToolName(evt.Response, 0);
        var toolArgs = ExtractToolArgs(evt.Response, 0);

        call.CurrentStep = SagaStep.ToolCallRequested;
        await context.SaveChangesAsync(ct);

        await eventBus.PublishAsync(new ToolCallRequested(
            evt.SagaId, 0, toolName, toolArgs), ct);
    }

    private string ExtractToolName(string response, int index)
    {
        var pattern = "\"name\":";
        var pos = 0;
        for (int i = 0; i <= index; i++)
        {
            pos = response.IndexOf(pattern, pos, StringComparison.Ordinal);
            if (pos < 0) break;
            pos += pattern.Length;
        }
        if (pos < 0) return "unknown";

        var start = response.IndexOf('"', pos) + 1;
        var end = response.IndexOf('"', start);
        return response.Substring(start, end - start);
    }

    private string ExtractToolArgs(string response, int index)
    {
        var pattern = "\"arguments\":";
        var pos = 0;
        for (int i = 0; i <= index; i++)
        {
            pos = response.IndexOf(pattern, pos, StringComparison.Ordinal);
            if (pos < 0) break;
            pos += pattern.Length;
        }
        if (pos < 0) return "{}";

        var start = response.IndexOf('{', pos);
        if (start < 0) return "{}";

        var depth = 0;
        var end = start;
        for (int i = start; i < response.Length; i++)
        {
            if (response[i] == '{') depth++;
            if (response[i] == '}') depth--;
            if (depth == 0) { end = i + 1; break; }
        }
        return response.Substring(start, end - start);
    }
}
```

**Step 2: Verify syntax**

Run: `cd src/Adnd.Server && dotnet build --no-restore 2>&1 | grep -i error`
Expected: no output

**Step 3: Commit**

```bash
git add src/Adnd.Server/Handlers/LLMResponseHandler.cs
git commit -m "feat: add LLMResponseHandler for LLMResponseReceived"
```

---

### Task 7: Create ToolExecutionHandler

**Files:**
- Create: `src/Adnd.Server/Handlers/ToolExecutionHandler.cs`

**Step 1: Create the handler**

```csharp
using Adnd.Server.Data;
using Adnd.Server.Events;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Adnd.Server.Handlers;

public class ToolExecutionHandler : IEventHandler<ToolCallRequested>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ToolExecutionHandler> _logger;

    public ToolExecutionHandler(IServiceProvider serviceProvider, ILogger<ToolExecutionHandler> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task HandleAsync(ToolCallRequested evt, CancellationToken ct)
    {
        _logger.LogInformation("[TOOL] Executing | SagaId={SagaId} | Index={Index} | Tool={Tool}",
            evt.SagaId, evt.ToolIndex, evt.ToolName);

        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var call = await context.AgentCalls.FirstOrDefaultAsync(c => c.Id == evt.SagaId, ct);
        if (call == null)
        {
            _logger.LogWarning("[TOOL] CallNotFound | SagaId={SagaId}", evt.SagaId);
            return;
        }

        // Execute the tool
        var toolRegistry = scope.ServiceProvider.GetRequiredService<IGMToolRegistry>();
        var result = await toolRegistry.ExecuteToolAsync(call.GameId, call.SessionId ?? Guid.Empty, evt.ToolName, evt.ToolArgs);

        var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();

        if (result.RequiresUserInput)
        {
            // Tool needs confirmation — save state and emit waiting event
            call.CurrentStep = SagaStep.ToolCallRequested;
            call.LastGMAction = $"ToolCall: {evt.ToolName} (waiting confirmation)";
            call.LastGMActionAt = DateTime.UtcNow;
            await context.SaveChangesAsync(ct);

            await eventBus.PublishAsync(new AgentCallFailed(evt.SagaId, $"Waiting user input for {evt.ToolName}"), ct);
            return;
        }

        // Update coordinator if one exists
        var coordinator = await context.ToolCallCoordinators
            .FirstOrDefaultAsync(c => c.SagaId == evt.SagaId && c.Status == CoordinatorStatus.Active, ct);

        if (coordinator != null)
        {
            var toolResult = new ToolCallResult
            {
                Index = evt.ToolIndex,
                ToolName = evt.ToolName,
                Result = result.Output ?? "",
                Error = result.Error
            };
            coordinator.CompletedTools.Add(toolResult);
            coordinator.CurrentIndex = evt.ToolIndex + 1;

            if (evt.ToolIndex + 1 >= coordinator.TotalTools)
            {
                coordinator.Status = CoordinatorStatus.Completed;
                coordinator.CompletedAt = DateTime.UtcNow;
            }

            await context.SaveChangesAsync(ct);
        }

        // Emit ToolCallCompleted — CoordinatorHandler will listen and emit next tool or follow-up
        await eventBus.PublishAsync(new ToolCallCompleted(
            evt.SagaId, evt.ToolIndex, result.Output ?? "", result.Error), ct);

        _logger.LogInformation("[TOOL] Executed | SagaId={SagaId} | Index={Index} | Success={Success}",
            evt.SagaId, evt.ToolIndex, result.Success);
    }
}
```

**Step 2: Verify syntax**

Run: `cd src/Adnd.Server && dotnet build --no-restore 2>&1 | grep -i error`
Expected: no output

**Step 3: Commit**

```bash
git add src/Adnd.Server/Handlers/ToolExecutionHandler.cs
git commit -m "feat: add ToolExecutionHandler for ToolCallRequested"
```

---

### Task 8: Create CoordinatorHandler

**Files:**
- Create: `src/Adnd.Server/Handlers/CoordinatorHandler.cs`

**Step 1: Create the handler**

```csharp
using Adnd.Server.Data;
using Adnd.Server.Events;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Adnd.Server.Handlers;

public class CoordinatorHandler : IEventHandler<ToolCallCompleted>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CoordinatorHandler> _logger;

    public CoordinatorHandler(IServiceProvider serviceProvider, ILogger<CoordinatorHandler> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task HandleAsync(ToolCallCompleted evt, CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var coordinator = await context.ToolCallCoordinators
            .FirstOrDefaultAsync(c => c.SagaId == evt.SagaId && c.Status == CoordinatorStatus.Active, ct);

        if (coordinator == null)
        {
            // No coordinator — nothing to track, done
            return;
        }

        // Check if all tools are done
        if (coordinator.CurrentIndex >= coordinator.TotalTools)
        {
            coordinator.Status = CoordinatorStatus.Completed;
            coordinator.CompletedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(ct);

            // Emit follow-up LLM event with tool results
            var toolResults = coordinator.CompletedTools
                .OrderBy(t => t.Index)
                .Select(t => new ToolResult(t.Index, t.ToolName, t.Result, t.Error))
                .ToList();

            var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();
            await eventBus.PublishAsync(new LLMFollowUpRequested(evt.SagaId, toolResults), ct);

            _logger.LogInformation("[COORD] AllToolsDone | SagaId={SagaId} | Tools={Count}", evt.SagaId, toolResults.Count);
            return;
        }

        // Emit next tool call
        var call = await context.AgentCalls.FirstOrDefaultAsync(c => c.Id == evt.SagaId, ct);
        if (call == null) return;

        // Extract next tool name/args from the original AgentCall input (which contains the LLM response with tool calls)
        // For now, we need to re-parse the LLM response. We'll store tool info in the coordinator.
        // Actually, the coordinator needs to store the tool calls. Let's update it.

        var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();
        await eventBus.PublishAsync(new CoordinatorUpdated(evt.SagaId, coordinator.CurrentIndex, coordinator.TotalTools), ct);

        _logger.LogInformation("[COORD] NextTool | SagaId={SagaId} | Index={Index}/{Total}",
            evt.SagaId, coordinator.CurrentIndex, coordinator.TotalTools);
    }
}
```

**Note:** This handler has an issue — it needs to know the next tool's name and args. We need to update the coordinator model to store tool calls. Let me revise:

**Revised Step 1: Update ToolCallCoordinator model**

Add `Tools` column to `ToolCallCoordinator`:

```csharp
public string? ToolsJson { get; set; }  // JSON: [{name, arguments}, ...]
```

**Revised Step 1b: Update CoordinatorHandler**

```csharp
using Adnd.Server.Data;
using Adnd.Server.Events;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Adnd.Server.Handlers;

public class CoordinatorHandler : IEventHandler<ToolCallCompleted>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CoordinatorHandler> _logger;

    public CoordinatorHandler(IServiceProvider serviceProvider, ILogger<CoordinatorHandler> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task HandleAsync(ToolCallCompleted evt, CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var coordinator = await context.ToolCallCoordinators
            .FirstOrDefaultAsync(c => c.SagaId == evt.SagaId && c.Status == CoordinatorStatus.Active, ct);

        if (coordinator == null) return;

        if (coordinator.CurrentIndex >= coordinator.TotalTools)
        {
            coordinator.Status = CoordinatorStatus.Completed;
            coordinator.CompletedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(ct);

            var toolResults = coordinator.CompletedTools
                .OrderBy(t => t.Index)
                .Select(t => new ToolResult(t.Index, t.ToolName, t.Result, t.Error))
                .ToList();

            var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();
            await eventBus.PublishAsync(new LLMFollowUpRequested(evt.SagaId, toolResults), ct);

            _logger.LogInformation("[COORD] AllToolsDone | SagaId={SagaId} | Tools={Count}", evt.SagaId, toolResults.Count);
            return;
        }

        // Parse tools from JSON
        var tools = JsonSerializer.Deserialize<List<ToolCallInfo>>(coordinator.ToolsJson ?? "[]") ?? new();
        var nextTool = tools[coordinator.CurrentIndex];

        var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();
        await eventBus.PublishAsync(new ToolCallRequested(
            evt.SagaId, coordinator.CurrentIndex, nextTool.Name, nextTool.Arguments), ct);

        _logger.LogInformation("[COORD] NextTool | SagaId={SagaId} | Index={Index}/{Total}",
            evt.SagaId, coordinator.CurrentIndex, coordinator.TotalTools);
    }
}

public class ToolCallInfo
{
    public string Name { get; set; } = "";
    public string Arguments { get; set; } = "";
}
```

**Step 2: Update LLMResponseHandler to store tools in coordinator**

In `LLMResponseHandler.HandleAsync`, when creating the coordinator, add:

```csharp
// Extract tool calls from response
var toolCalls = ExtractToolCalls(evt.Response);
coordinator.ToolsJson = JsonSerializer.Serialize(toolCalls);
```

Add helper method:

```csharp
private List<ToolCallInfo> ExtractToolCalls(string response)
{
    var tools = new List<ToolCallInfo>();
    // Parse tool calls from the LLM response
    // Use a proper JSON parser if the response is valid JSON
    // Otherwise use regex for tool call patterns
    try
    {
        var doc = JsonDocument.Parse(response);
        if (doc.RootElement.TryGetProperty("tool_calls", out var tcProp))
        {
            foreach (var tc in tcProp.EnumerateArray())
            {
                tools.Add(new ToolCallInfo
                {
                    Name = tc.GetProperty("name").GetString() ?? "",
                    Arguments = tc.GetProperty("arguments").ToString()
                });
            }
        }
    }
    catch
    {
        // Fallback: regex-based extraction
        tools = ExtractToolCallsRegex(response);
    }
    return tools;
}

private List<ToolCallInfo> ExtractToolCallsRegex(string response)
{
    var tools = new List<ToolCallInfo>();
    var namePattern = new System.Text.RegularExpressions.Regex(@"""name""\s*:\s*""([^""]+)""");
    var argsPattern = new System.Text.RegularExpressions.Regex(@"""arguments""\s*:\s*(\{[^}]+\})");

    var names = namePattern.Matches(response);
    var args = argsPattern.Matches(response);

    for (int i = 0; i < names.Count; i++)
    {
        if (i < args.Count)
        {
            tools.Add(new ToolCallInfo
            {
                Name = names[i].Groups[1].Value,
                Arguments = args[i].Groups[1].Value
            });
        }
    }
    return tools;
}
```

**Step 3: Verify syntax**

Run: `cd src/Adnd.Server && dotnet build --no-restore 2>&1 | grep -i error`
Expected: no output

**Step 4: Commit**

```bash
git add src/Adnd.Server/Models/ToolCallCoordinator.cs src/Adnd.Server/Handlers/CoordinatorHandler.cs src/Adnd.Server/Handlers/LLMResponseHandler.cs
git commit -m "feat: add CoordinatorHandler and persist tool calls in coordinator"
```

---

### Task 9: Create LLMFollowUpHandler

**Files:**
- Create: `src/Adnd.Server/Handlers/LLMFollowUpHandler.cs`

**Step 1: Create the handler**

```csharp
using Adnd.Server.Data;
using Adnd.Server.Events;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Adnd.Server.Handlers;

public class LLMFollowUpHandler : IEventHandler<LLMFollowUpRequested>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<LLMFollowUpHandler> _logger;

    public LLMFollowUpHandler(IServiceProvider serviceProvider, ILogger<LLMFollowUpHandler> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task HandleAsync(LLMFollowUpRequested evt, CancellationToken ct)
    {
        _logger.LogInformation("[LLM] FollowUp | SagaId={SagaId} | ToolResults={Count}", evt.SagaId, evt.ToolResults.Count);

        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var call = await context.AgentCalls.FirstOrDefaultAsync(c => c.Id == evt.SagaId, ct);
        if (call?.Game == null || call.Game.LLMPreset == null)
        {
            await PublishFailure(scope, evt.SagaId, "LLM preset not configured", context, ct);
            return;
        }

        var provider = GetProvider(call.Game.LLMPreset);
        if (provider == null)
        {
            await PublishFailure(scope, evt.SagaId, $"LLM provider '{call.Game.LLMPreset.ProviderType}' not available", context, ct);
            return;
        }

        // Build tool results text
        var toolResultsText = string.Join("\n", evt.ToolResults.Select(tr =>
            $"Tool '{tr.ToolName}': {tr.Result}"));
        var userPrompt = $"Tool results:\n{toolResultsText}\n\nNow continue the narrative based on these results.";

        string result;
        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            result = await provider.CompleteAsync(call.Game.LLMPreset.ProviderType == "Ollama" ? "You are the Game Master." : "You are the Game Master.", userPrompt, evt.ToolResults.Any() ? null : null);
            sw.Stop();

            var interactionLogger = scope.ServiceProvider.GetRequiredService<ILLMInteractionLogger>();
            var tokenUsage = provider.GetTokenUsage(result);
            await interactionLogger.LogInteractionAsync(
                call.Game.CreatorId, call.Game.LLMPresetId, provider.ProviderId,
                evt.ToolResults.Any() ? null : call.Game.LLMPreset.BaseModel,
                tokenUsage?.promptTokens, tokenUsage?.completionTokens, tokenUsage?.totalTokens,
                (int)sw.ElapsedMilliseconds, "You are the Game Master.", userPrompt, result,
                null, provider.EndpointUrl, "saga", call.GameId, call.SessionId,
                "LLMFollowUpHandler", "follow_up",
                call.Game.LLMPreset.Name, call.Game.LLMPreset.EndpointUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[LLM] FollowUpFailed | SagaId={SagaId}", evt.SagaId);
            await PublishFailure(scope, evt.SagaId, ex.Message, context, ct);
            return;
        }

        var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();
        await eventBus.PublishAsync(new NarrativeReady(evt.SagaId, result), ct);

        call.CurrentStep = SagaStep.NarrativeReady;
        await context.SaveChangesAsync(ct);

        _logger.LogInformation("[LLM] FollowUpDone | SagaId={SagaId}", evt.SagaId);
    }

    private ILLMProvider? GetProvider(LLMPreset preset)
    {
        var factory = _serviceProvider.GetRequiredService<ILLMProviderFactory>();
        return factory.CreateFromPreset(preset);
    }

    private async Task PublishFailure(IServiceScope scope, Guid sagaId, string error, AppDbContext context, CancellationToken ct)
    {
        var call = await context.AgentCalls.FirstOrDefaultAsync(c => c.Id == sagaId, ct);
        if (call != null)
        {
            call.Status = AgentCallStatus.Failed;
            call.Error = error;
            call.CompletedAt = DateTime.UtcNow;
            call.CurrentStep = SagaStep.Failed;
            await context.SaveChangesAsync(ct);
        }

        var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();
        await eventBus.PublishAsync(new AgentCallFailed(sagaId, error), ct);
    }
}
```

**Step 2: Verify syntax**

Run: `cd src/Adnd.Server && dotnet build --no-restore 2>&1 | grep -i error`
Expected: no output

**Step 3: Commit**

```bash
git add src/Adnd.Server/Handlers/LLMFollowUpHandler.cs
git commit -m "feat: add LLMFollowUpHandler for LLMFollowUpRequested"
```

---

### Task 10: Create NarrativeHandler and AgentCallCompletedHandler

**Files:**
- Create: `src/Adnd.Server/Handlers/NarrativeHandler.cs`
- Create: `src/Adnd.Server/Handlers/AgentCallCompletedHandler.cs`

**Step 1: Create NarrativeHandler.cs**

```csharp
using Adnd.Server.Data;
using Adnd.Server.Events;
using Adnd.Server.Hubs;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Adnd.Server.Handlers;

public class NarrativeHandler : IEventHandler<NarrativeReady>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IHubContext<GameHub> _hubContext;
    private readonly ILogger<NarrativeHandler> _logger;

    public NarrativeHandler(IServiceProvider serviceProvider, IHubContext<GameHub> hubContext, ILogger<NarrativeHandler> logger)
    {
        _serviceProvider = serviceProvider;
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task HandleAsync(NarrativeReady evt, CancellationToken ct)
    {
        _logger.LogInformation("[NARRATIVE] Ready | SagaId={SagaId}", evt.SagaId);

        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var call = await context.AgentCalls.FirstOrDefaultAsync(c => c.Id == evt.SagaId, ct);
        if (call == null) return;

        call.CurrentStep = SagaStep.Completed;
        call.Output = evt.Narrative;
        call.OutputMessage = evt.Narrative;
        call.CompletedAt = DateTime.UtcNow;
        var startedAt = call.StartedAt ?? call.CreatedAt;
        call.DurationMs = (int)(call.CompletedAt.Value - startedAt).TotalMilliseconds;

        await context.SaveChangesAsync(ct);

        // Broadcast to game
        await _hubContext.Clients.Group(call.GameId.ToString()).SendAsync("GameNarration", new
        {
            call.GameId,
            call.SessionId,
            Content = evt.Narrative,
            MessageType = (int)MessageType.AgentResponse,
            Timestamp = DateTime.UtcNow
        });

        _logger.LogInformation("[NARRATIVE] Broadcast | SagaId={SagaId} | Len={Len}", evt.SagaId, evt.Narrative.Length);
    }
}
```

**Step 2: Create AgentCallCompletedHandler.cs**

```csharp
using Adnd.Server.Events;
using Adnd.Server.Services;
using Microsoft.Extensions.Logging;

namespace Adnd.Server.Handlers;

public class AgentCallCompletedHandler : IEventHandler<AgentCallCompleted>
{
    private readonly ILogger<AgentCallCompletedHandler> _logger;

    public AgentCallCompletedHandler(ILogger<AgentCallCompletedHandler> logger)
    {
        _logger = logger;
    }

    public Task HandleAsync(AgentCallCompleted evt, CancellationToken ct)
    {
        _logger.LogInformation("[SAGA] Completed | SagaId={SagaId}", evt.SagaId);
        return Task.CompletedTask;
    }
}
```

**Step 3: Create AgentCallFailedHandler.cs**

```csharp
using Adnd.Server.Events;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Adnd.Server.Handlers;

public class AgentCallFailedHandler : IEventHandler<AgentCallFailed>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AgentCallFailedHandler> _logger;

    public AgentCallFailedHandler(IServiceProvider serviceProvider, ILogger<AgentCallFailedHandler> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task HandleAsync(AgentCallFailed evt, CancellationToken ct)
    {
        _logger.LogError("[SAGA] Failed | SagaId={SagaId} | Error={Error}", evt.SagaId, evt.Error);

        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var call = await context.AgentCalls.FirstOrDefaultAsync(c => c.Id == evt.SagaId, ct);
        if (call == null) return;

        call.Status = AgentCallStatus.Failed;
        call.Error = evt.Error;
        call.CompletedAt = DateTime.UtcNow;
        call.CurrentStep = SagaStep.Failed;

        await context.SaveChangesAsync(ct);

        // Abandon any coordinators for this saga
        var coordinators = context.ToolCallCoordinators.Where(c => c.SagaId == evt.SagaId && c.Status == CoordinatorStatus.Active);
        foreach (var coord in coordinators)
        {
            coord.Status = CoordinatorStatus.Abandoned;
            coord.CompletedAt = DateTime.UtcNow;
        }
        await context.SaveChangesAsync(ct);
    }
}
```

**Step 4: Verify syntax**

Run: `cd src/Adnd.Server && dotnet build --no-restore 2>&1 | grep -i error`
Expected: no output

**Step 5: Commit**

```bash
git add src/Adnd.Server/Handlers/NarrativeHandler.cs src/Adnd.Server/Handlers/AgentCallCompletedHandler.cs src/Adnd.Server/Handlers/AgentCallFailedHandler.cs
git commit -m "feat: add NarrativeHandler, AgentCallCompletedHandler, and AgentCallFailedHandler"
```

---

## Phase 3: Rewrite GameAgent as RabbitMQ Consumer

### Task 11: Rewrite GameAgent.cs

**Files:**
- Overwrite: `src/Adnd.Server/Agent/GameAgent.cs`

**Step 1: Write the new GameAgent**

Replace the entire `GameAgent` class (keep `GameAgentManager` section for now — it'll be cleaned up later):

```csharp
using Microsoft.EntityFrameworkCore;
using Adnd.Server.Data;
using Adnd.Server.Events;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Framing;
using System.Text.Json;

namespace Adnd.Server.Agent;

public class GameAgent : IGameAgent
{
    private readonly Guid _gameId;
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GameAgent> _logger;
    private readonly CancellationTokenSource _cts = new();
    private IConnection? _rabbitMqConnection;
    private IModel? _channel;
    private AsyncEventingBasicConsumer? _consumer;
    private volatile bool _isPaused = false;
    private volatile bool _isConnected = false;

    public GameAgent(
        Guid gameId,
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<GameAgent> logger)
    {
        _gameId = gameId;
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task StartAsync(Guid gameId, Guid creatorId)
    {
        if (_isConnected)
        {
            _logger.LogWarning("Game agent already connected for game {GameId}", gameId);
            return;
        }

        // Activate the game in the DB
        var game = await GetGameAsync(gameId);
        if (game == null)
            throw new KeyNotFoundException($"Game {gameId} not found.");

        if (game.LLMPresetId == null)
            throw new InvalidOperationException("Cannot start: no LLM preset configured.");

        game.Status = Models.GameStatus.Starting;
        game.StartedAt = DateTime.UtcNow;
        game.GMStatus = Models.GMStatus.Running;

        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await context.SaveChangesAsync();

        _logger.LogInformation("Starting game agent for game {GameId}", gameId);

        // Connect to RabbitMQ
        ConnectToRabbitMq();

        // Declare agent queue
        DeclareAgentQueue();

        // Recover pending sagas
        await RecoverPendingSagas();

        // Start consuming
        StartConsuming();

        _isConnected = true;
    }

    private void ConnectToRabbitMq()
    {
        var host = _configuration["RabbitMq:Host"] ?? "localhost";
        var port = _configuration.GetValue<int>("RabbitMq:Port", 5672);
        var username = _configuration["RabbitMq:Username"] ?? "adnd";
        var password = _configuration["RabbitMq:Password"] ?? "adnd";
        var virtualHost = _configuration["RabbitMq:VirtualHost"] ?? "/adnd";

        var factory = new ConnectionFactory
        {
            HostName = host,
            Port = port,
            UserName = username,
            Password = password,
            VirtualHost = virtualHost
        };

        _rabbitMqConnection = factory.CreateConnection();
        _channel = _rabbitMqConnection.CreateModel();
        _channel.ExchangeDeclare("adnd.events", ExchangeType.Direct, durable: true);

        _logger.LogInformation("RabbitMQ connected for game {GameId}", _gameId);
    }

    private void DeclareAgentQueue()
    {
        var queueName = $"agent.{_gameId}";

        _channel!.QueueDeclare(queueName, durable: true, exclusive: false, autoDelete: false, null);
        _channel.QueueBind(queueName, "adnd.events", $"agent.{_gameId}");

        _logger.LogInformation("Declared agent queue {Queue} for game {GameId}", queueName, _gameId);
    }

    private void StartConsuming()
    {
        _consumer = new AsyncEventingBasicConsumer(_channel!);

        _consumer.Received += async (model, ea) =>
        {
            try
            {
                var body = ea.Body.ToArray();
                var payload = System.Text.Encoding.UTF8.GetString(body);
                var properties = ea.BasicProperties;
                var correlationId = properties?.CorrelationId;

                using var scope = _serviceProvider.CreateScope();

                // Deserialize event and dispatch to handlers via EventBusWorker
                var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();
                var evt = JsonSerializer.Deserialize<IGameEvent>(payload);
                if (evt != null)
                {
                    // Direct dispatch — do NOT call PublishAsync (that would re-publish to RabbitMQ)
                    await DispatchToHandlers(evt, payload, correlationId, scope.ServiceProvider, CancellationToken.None);
                }

                _channel!.BasicAck(ea.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message for game {GameId}", _gameId);
                _channel!.BasicNack(ea.DeliveryTag, multiple: false, requeue: true);
            }
        };

        _channel!.BasicConsume(
            queue: $"agent.{_gameId}",
            autoAck: false,
            consumer: _consumer!);

        _logger.LogInformation("Started consuming agent queue for game {GameId}", _gameId);
    }

    /// <summary>
    /// Dispatch an event to registered handlers. Called by the consumer on message receipt.
    /// </summary>
    private async Task DispatchToHandlers(IGameEvent evt, string payload, string? correlationId, IServiceProvider sp, CancellationToken ct)
    {
        using var scope = sp.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Persist as EventRecord
        var record = new EventRecord
        {
            Id = Guid.NewGuid(),
            GameId = evt.GameId,
            EventType = evt.GetType().FullName!,
            Payload = payload,
            Status = EventStatus.Published,
            CorrelationId = correlationId ?? Guid.NewGuid().ToString(),
            CreatedAt = DateTime.UtcNow,
            PublishedAt = DateTime.UtcNow
        };
        context.EventRecords.Add(record);
        await context.SaveChangesAsync(ct);

        // Dispatch to handlers
        var registry = scope.ServiceProvider.GetRequiredService<IHandlerRegistry>();
        var key = evt.GetType().FullName!;

        if (!registry.Handlers.TryGetValue(key, out var handlers))
        {
            _logger.LogDebug("[EVENT] NoHandlerForType | EventType={EventType} | EventId={EventId}",
                key, record.Id);
            record.Status = EventStatus.Acknowledged;
            record.AckedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(ct);
            return;
        }

        var success = true;
        foreach (var handlerType in handlers)
        {
            try
            {
                using var handlerScope = sp.CreateScope();
                var handlerInstance = ActivatorUtilities.CreateInstance(handlerScope.ServiceProvider, handlerType);

                var handleMethod = handlerType.GetMethods()
                    .FirstOrDefault(m => m.Name == "HandleAsync"
                                         && m.GetParameters().Length >= 1
                                         && m.GetParameters()[0].ParameterType == evt.GetType());

                if (handleMethod == null)
                {
                    _logger.LogError("[EVENT] NoHandleAsync | Handler={HandlerType} | EventType={EventType}",
                        handlerType.Name, key);
                    success = false;
                    continue;
                }

                var task = (Task)handleMethod.Invoke(handlerInstance, new object[] { evt, ct })!;
                await task;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[EVENT] HandlerException | Handler={Handler} | EventType={EventType} | EventId={EventId}",
                    handlerType.Name, key, record.Id);
                success = false;
            }
        }

        record.Status = success ? EventStatus.Acknowledged : EventStatus.Failed;
        record.AckedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(ct);
    }

    private async Task RecoverPendingSagas()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var pendingSagas = await context.AgentCalls
            .Where(c => c.GameId == _gameId &&
                       c.Status != AgentCallStatus.Completed &&
                       c.Status != AgentCallStatus.Failed &&
                       c.Status != AgentCallStatus.Cancelled)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();

        if (pendingSagas.Count == 0)
        {
            _logger.LogInformation("No pending sagas for game {GameId}", _gameId);
            return;
        }

        _logger.LogInformation("Recovering {Count} sagas for game {GameId}", pendingSagas.Count, _gameId);

        foreach (var call in pendingSagas)
        {
            // Check for coordinator
            var coordinator = await context.ToolCallCoordinators
                .FirstOrDefaultAsync(c => c.SagaId == call.Id && c.Status == CoordinatorStatus.Active);

            if (coordinator != null && coordinator.CompletedTools.Any())
            {
                // Resume from coordinator state
                var tools = JsonSerializer.Deserialize<List<ToolCallInfo>>(coordinator.ToolsJson) ?? new();
                if (coordinator.CurrentIndex < coordinator.TotalTools)
                {
                    var nextTool = tools[coordinator.CurrentIndex];
                    var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();
                    await eventBus.PublishAsync(new ToolCallRequested(
                        call.Id, coordinator.CurrentIndex, nextTool.Name, nextTool.Arguments));
                    _logger.LogInformation("Resumed saga {SagaId} from tool {Index}", call.Id, coordinator.CurrentIndex);
                }
                else if (coordinator.Status == CoordinatorStatus.Active)
                {
                    // All tools done but coordinator still active — emit follow-up
                    var toolResults = coordinator.CompletedTools
                        .OrderBy(t => t.Index)
                        .Select(t => new ToolResult(t.Index, t.ToolName, t.Result, t.Error))
                        .ToList();
                    var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();
                    await eventBus.PublishAsync(new LLMFollowUpRequested(call.Id, toolResults));
                    _logger.LogInformation("Resumed saga {SagaId} with follow-up LLM", call.Id);
                }
            }
            else
            {
                // No coordinator — emit AgentCallQueued to restart saga
                var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();
                await eventBus.PublishAsync(new AgentCallQueued(call.Id, call.GameId));
                _logger.LogInformation("Resumed saga {SagaId} from AgentCallQueued", call.Id);
            }
        }
    }

    public async Task PauseAsync(Guid gameId)
    {
        _isPaused = true;
        _logger.LogInformation("Pausing game agent for game {GameId}", gameId);

        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var game = await context.Games.FindAsync(gameId);
        if (game != null)
        {
            game.GMStatus = Models.GMStatus.Paused;
            game.LastGMAction = "Paused";
            game.LastGMActionAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
        }
    }

    public async Task ResumeAsync(Guid gameId)
    {
        _isPaused = false;
        _logger.LogInformation("Resuming game agent for game {GameId}", gameId);

        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var game = await context.Games.FindAsync(gameId);
        if (game != null)
        {
            game.GMStatus = Models.GMStatus.Running;
            game.LastGMAction = "Resumed";
            game.LastGMActionAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
        }
    }

    public async Task<GMStatus> GetStatusAsync(Guid gameId)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var game = await context.Games.FindAsync(gameId);
        if (game == null)
            throw new KeyNotFoundException($"Game {gameId} not found.");
        return game.GMStatus;
    }

    public bool IsActive(Guid gameId)
    {
        return _isConnected && !_isPaused;
    }

    public IEnumerable<Guid> GetActiveGameIds()
    {
        yield return _gameId;
    }

    private async Task<Game?> GetGameAsync(Guid gameId)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await context.Games.FindAsync(gameId);
    }

    public void Dispose()
    {
        _cts.Cancel();
        _consumer?.Dispose();
        _channel?.Dispose();
        _rabbitMqConnection?.Close();
        _rabbitMqConnection?.Dispose();
        _cts.Dispose();
        _isConnected = false;
    }
}
```

**Step 2: Verify syntax**

Run: `cd src/Adnd.Server && dotnet build --no-restore 2>&1 | grep -i error`
Expected: no output (or expected errors for removed interfaces — we'll fix those next)

**Step 3: Commit**

```bash
git add src/Adnd.Server/Agent/GameAgent.cs
git commit -m "refactor: rewrite GameAgent as RabbitMQ consumer (no polling loop)"
```

---

### Task 12: Clean up IGameAgent interface

**Files:**
- Modify: `src/Adnd.Server/Services/IGameAgent.cs`

**Step 1: Remove EnqueueCall and OnAgentCallQueued from IGameAgent**

Remove these methods from the interface:
```csharp
void EnqueueCall(Guid callId);
```

Remove `OnAgentCallQueued` from `IGameAgentManager`:
```csharp
void OnAgentCallQueued(Guid gameId, Guid callId);
```

**Step 2: Verify syntax**

Run: `cd src/Adnd.Server && dotnet build --no-restore 2>&1 | grep -i error`
Expected: errors for GameAgentManager (still references removed methods)

**Step 3: Commit**

```bash
git add src/Adnd.Server/Services/IGameAgent.cs
git commit -m "refactor: remove EnqueueCall and OnAgentCallQueued from interfaces"
```

---

### Task 13: Clean up GameAgentManager

**Files:**
- Modify: `src/Adnd.Server/Agent/GameAgent.cs` (keep GameAgentManager class at bottom)

**Step 1: Remove dead methods from GameAgentManager**

Remove from `GameAgentManager`:
- `OnAgentCallQueued()` method
- `EnqueuePendingCallsAsync()` method
- Call to `EnqueuePendingCallsAsync()` in `StartAllActiveGamesAsync()`

**Step 2: Update GameAgent constructor in GetOrCreate**

Change from:
```csharp
var agent = new GameAgent(gameId, _scopeFactory, gameEngine, ragService, systemRegistry, agentLogger);
```
To:
```csharp
var agent = new GameAgent(gameId, _serviceProvider, _configuration, agentLogger);
```

**Step 3: Verify syntax**

Run: `cd src/Adnd.Server && dotnet build --no-restore 2>&1 | grep -i error`
Expected: no output

**Step 4: Commit**

```bash
git add src/Adnd.Server/Agent/GameAgent.cs
git commit -m "refactor: clean up GameAgentManager (remove OnAgentCallQueued, EnqueuePendingCallsAsync)"
```

---

## Phase 4: EventBusWorker & AgentBus Cleanup

### Task 14: Remove AgentCallQueued special case from EventBusWorker

**Files:**
- Modify: `src/Adnd.Server/Services/EventBusWorker.cs`

**Step 1: Remove the special-case branch in DispatchToHandlers**

Remove this entire block from `DispatchToHandlers`:
```csharp
// Special handling: AgentCallQueued events must wake up the GameAgent
if (record.EventType == typeof(Events.AgentCallQueued).FullName)
{
    try
    {
        var callEvt = JsonSerializer.Deserialize(record.Payload, typeof(Events.AgentCallQueued)) as Events.AgentCallQueued;
        if (callEvt != null)
        {
            _gameAgentManager.OnAgentCallQueued(callEvt.GameId, callEvt.CallId);
        }
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "[EVENT] FailedToWakeGameAgent | EventType={EventType} | EventId={EventId}",
            record.EventType, record.Id);
    }
}
```

**Step 2: Remove `_gameAgentManager` dependency from constructor**

Remove the `IGameAgentManager gameAgentManager` parameter from the constructor and the field.

**Step 3: Verify syntax**

Run: `cd src/Adnd.Server && dotnet build --no-restore 2>&1 | grep -i error`
Expected: no output

**Step 4: Commit**

```bash
git add src/Adnd.Server/Services/EventBusWorker.cs
git commit -m "refactor: remove AgentCallQueued special case from EventBusWorker"
```

---

### Task 15: Update AgentBus to publish via IEventBus

**Files:**
- Modify: `src/Adnd.Server/Services/AgentBus.cs`

**Step 1: Replace direct call with event bus publish**

In `SendCallAsync`, find:
```csharp
_gameAgentManager.OnAgentCallQueued(call.GameId, call.Id);
```

Replace with:
```csharp
await _eventBus.PublishAsync(new AgentCallQueued(call.Id, call.GameId));
```

**Note:** The `AgentCallQueued` event will now go through the full pipeline:
1. Persisted to EventRecords table
2. Published to RabbitMQ `agent.{gameId}` queue
3. GameAgent consumer receives it → dispatches to SagaOrchestratorHandler
4. SagaOrchestratorHandler reads the AgentCall, determines action, emits next event

This is the correct flow — the event bus handles persistence + routing.

**Step 2: Verify syntax**

Run: `cd src/Adnd.Server && dotnet build --no-restore 2>&1 | grep -i error`
Expected: no output

**Step 3: Commit**

```bash
git add src/Adnd.Server/Services/AgentBus.cs
git commit -m "refactor: AgentBus publishes AgentCallQueued via IEventBus instead of direct call"
```

---

## Phase 5: EF Migration & Integration

### Task 16: Create EF migration

**Files:**
- Create: `src/Adnd.Server/Data/Migrations/` (auto-generated)

**Step 1: Run EF migration command**

```bash
cd src/Adnd.Server
dotnet ef migrations add ReactiveSagaArchitecture
```

This should generate:
- `CurrentStep` column on `AgentCalls` table
- `ToolCallCoordinators` table
- `ToolsJson` column on `ToolCallCoordinators`

**Step 2: Verify migration**

Read the generated migration file and verify:
- `CurrentStep` has a default of 1 (Init)
- `ToolCallCoordinators` has proper foreign keys
- `ToolsJson` is a text column

**Step 3: Commit**

```bash
git add src/Adnd.Server/Data/Migrations/
git commit -m "db: add ReactiveSagaArchitecture migration (CurrentStep, ToolCallCoordinators)"
```

---

### Task 17: Update Program.cs registrations

**Files:**
- Modify: `src/Adnd.Server/Program.cs`

**Step 1: Update GameAgent registration**

Find:
```csharp
builder.Services.AddSingleton<IGameAgentManager, GameAgentManager>();
```

No change needed — GameAgent is created by GameAgentManager.

**Step 2: Verify everything compiles**

Run: `cd src/Adnd.Server && dotnet build 2>&1 | tail -20`
Expected: Build succeeded

**Step 3: Commit**

```bash
git add src/Adnd.Server/Program.cs
git commit -m "chore: verify Program.cs registrations for reactive saga architecture"
```

---

## Phase 6: Testing

### Task 18: Integration test — full saga flow

**Files:**
- Create: `tests/Adnd.Server.Tests/Handlers/SagaFlowTests.cs`

**Step 1: Create test project if it doesn't exist**

```bash
cd src
dotnet new xunit -n Adnd.Server.Tests
```

**Step 2: Write integration test**

```csharp
using Adnd.Server.Data;
using Adnd.Server.Events;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Adnd.Server.Tests.Handlers;

public class SagaFlowTests
{
    [Fact]
    public async Task AgentCallQueued_triggers_saga_chain()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase("SagaTest")
            .Options;

        using var context = new AppDbContext(options);
        var sagaId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        var call = new AgentCall
        {
            Id = sagaId,
            GameId = gameId,
            Action = AgentAction.Narrate,
            Input = "Test narrative",
            Status = AgentCallStatus.Pending,
            CurrentStep = SagaStep.Init
        };
        context.AgentCalls.Add(call);
        await context.SaveChangesAsync();

        // Act — simulate AgentCallQueued event
        var sagaId2 = call.Id;
        var game = new Game { Id = gameId, Status = GameStatus.Active, GMStatus = GMStatus.Running };
        context.Games.Add(game);
        await context.SaveChangesAsync();

        // Assert
        var retrieved = await context.AgentCalls.FindAsync(sagaId);
        Assert.NotNull(retrieved);
        Assert.Equal(SagaStep.LLMDispatchRequested, retrieved.CurrentStep);
    }
}
```

**Step 3: Run tests**

```bash
cd tests/Adnd.Server.Tests
dotnet test --filter "SagaFlowTests" -v n
```

**Step 4: Commit**

```bash
git add tests/Adnd.Server.Tests/Handlers/SagaFlowTests.cs
git commit -m "test: add saga flow integration test"
```

---

## Summary of All Changes

| Phase | Tasks | Files |
|-------|-------|-------|
| 1: Foundation | 1-3 | GameEvents.cs, AgentCall.cs, ToolCallCoordinator.cs, AppDbContext.cs, IEventHandler.cs, HandlerRegistry.cs, Program.cs |
| 2: Handlers | 4-10 | SagaOrchestratorHandler.cs, LLMDispatchHandler.cs, LLMResponseHandler.cs, ToolExecutionHandler.cs, CoordinatorHandler.cs, LLMFollowUpHandler.cs, NarrativeHandler.cs, AgentCallCompletedHandler.cs, AgentCallFailedHandler.cs |
| 3: GameAgent | 11-13 | GameAgent.cs (rewrite), IGameAgent.cs (cleanup), GameAgentManager.cs (cleanup) |
| 4: Cleanup | 14-15 | EventBusWorker.cs, AgentBus.cs |
| 5: Migration | 16-17 | EF migration, Program.cs |
| 6: Testing | 18 | SagaFlowTests.cs |

**Total: ~18 tasks, ~11 files created, ~6 files modified**

---

## Execution Handoff

Plan complete and saved to `docs/plans/2026-06-13-reactive-saga-implementation.md`. Two execution options:

**1. Subagent-Driven (this session)** — I dispatch fresh subagent per task, review between tasks, fast iteration

**2. Parallel Session (separate)** — Open new session with executing-plans, batch execution with checkpoints

**Which approach?**
