# Plan: Finish MassTransit Migration (Eliminate RabbitMQ.Client)

**Date:** 2026-06-14  
**Branch:** `feature/masstransit-bus`  
**Status:** Session 1 Complete — Session 2 needed

---

## Current State

### Already Done (on branch)
- ✅ MassTransit 8.3.6 + MassTransit.RabbitMQ packages added
- ✅ RabbitMQ.Client upgraded from 6.8.1 → 7.0.0 (required by MT 8.x) — **REMOVED** (no longer needed)
- ✅ 7 MassTransit consumers written (`Consumers/` directory)
- ✅ `AgentSagaData` model — inherits `ISaga` (needs update to `SagaStateMachineInstance`)
- ✅ `SagaDbContext` — normal DbContext (needs update to `MassTransit.EntityFramework.SagaDbContext`)
- ✅ **Phase 2: All 7 handlers migrated** from `RabbitMqEventBus.PublishToAgent()` → `IPublishEndpoint.Publish()`
  - CoordinatorHandler.cs, ToolExecutionHandler.cs, LLMDispatchHandler.cs
  - LLMFollowUpHandler.cs, LLMResponseHandler.cs, SagaOrchestratorHandler.cs
  - AgentBus.cs
- ✅ **Phase 5: RabbitMqEventBus.cs deleted** (was already deleted)
- ✅ **Phase 6: RabbitMQ.Client package removed** from csproj
- ✅ Removed dead `RabbitMqEventBus` DI registration from `Program.cs`
- ✅ Added `using MassTransit;` to all handler files
- ✅ Simplified Publish calls — removed unnecessary `x-event-type` headers
- ✅ Build succeeds: 0 errors

### Still Needs Work
- ❌ `SagaDbContext` — extends `DbContext` instead of `MassTransit.EntityFramework.SagaDbContext`
- ❌ `AgentSagaData` — inherits `ISaga` instead of `SagaStateMachineInstance`, missing `RowVersion`
- ❌ `AgentSaga` state machine — **file doesn't exist** — must be created
- ❌ Saga registration — commented out in `Program.cs`
- ❌ `GameAgent.cs` — 531 lines of direct RabbitMQ code (Phase 3: deferred)
- ❌ `AgentBus.cs` — still has `_gameAgentManager` field (dead, can be cleaned up)

---

## Session 1 Summary

### What was done
1. Replaced all `RabbitMqEventBus.PublishToAgent(gameId, payload, id, headers)` calls with `IPublishEndpoint.Publish(evt, ct)` in 7 files
2. Removed `RabbitMQ.Client` package reference (MassTransit.RabbitMQ provides its own)
3. Removed dead `RabbitMqEventBus` DI registration
4. Build passes cleanly

### What was blocked on
- **Phase 4 (Saga registration):** `AgentSaga` state machine class doesn't exist. The original plan's note about `ISagaStateMachineInstance` being internal was outdated — MassTransit 8.x uses `SagaStateMachineInstance` from `Automatonymous` namespace.
- **Phase 3 (GameAgent removal):** `IGameAgentManager` still actively used by `GameLifecycleHandler` and `EventBusWorker` for startup recovery. Removing requires refactoring these callers first.

---

## Session 2 Implementation Plan

### Step 1: Fix `SagaDbContext`
Extend `MassTransit.EntityFramework.SagaDbContext` (not EF Core's `DbContext`):

```csharp
using MassTransit.EntityFrameworkCoreIntegration;
using Adnd.Server.Models;

public class SagaDbContext : SagaDbContext
{
    public SagaDbContext(DbContextOptions<SagaDbContext> options) : base(options) { }
    public DbSet<AgentSagaData> AgentSagas { get; set; } = null!;

    protected override IEnumerable<ISagaClassMap> Configurations =>
        new[] { new AgentSagaClassMap() };
}

public class AgentSagaClassMap : SagaClassMap<AgentSagaData>
{
    protected override void Configure(EntityTypeBuilder<AgentSagaData> entity, ModelBuilder model)
    {
        entity.Property(s => s.CurrentState).HasMaxLength(64);
        entity.Property(s => s.RowVersion).IsRowVersion();
    }
}
```

### Step 2: Update `AgentSagaData`
Inherit `SagaStateMachineInstance` and add `RowVersion`:

```csharp
public class AgentSagaData : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; } = string.Empty;
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public Guid? AgentCallId { get; set; }
    public Guid? GameId { get; set; }
    public int ToolsRemaining { get; set; }
    public string? CurrentToolId { get; set; }
    public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;
}
```

### Step 3: Create `AgentSaga` state machine
Create `src/Adnd.Server/Services/AgentSaga.cs`:

```csharp
public class AgentSaga :
    MassTransitStateMachine<AgentSagaData>,
    IConsumer<AgentCallQueued>,
    IConsumer<LLMResponseReceived>,
    IConsumer<ToolCallCompleted>
{
    public State Orchestrate { get; private set; }
    public State ExecuteTools { get; private set; }
    public State FollowUpLLM { get; private set; }
    public State Complete { get; private set; }

    public Event<AgentCallQueued> AgentCallQueued { get; private set; }
    public Event<LLMResponseReceived> LLMResponseReceived { get; private set; }
    public Event<ToolCallCompleted> ToolCallCompleted { get; private set; }

    public AgentSaga()
    {
        InstanceState(x => x.CurrentState);
        Event(() => AgentCallQueued, cfg => { cfg.CorrelateById(ctx => ctx.Message.SagaId); cfg.ConfigureConsumeTopology = false; });
        Event(() => LLMResponseReceived, cfg => { cfg.CorrelateById(ctx => ctx.Message.SagaId); cfg.ConfigureConsumeTopology = false; });
        Event(() => ToolCallCompleted, cfg => { cfg.CorrelateById(ctx => ctx.Message.SagaId); cfg.ConfigureConsumeTopology = false; });

        Initially()
            .TransitionTo(Orchestrate);

        Orchestrate
            .OnEntry(ctx => Task.CompletedTask)
            .When(LLMResponseReceived)
                .If(ctx => ctx.Message.HasToolCalls, then: TransitionTo(ExecuteTools))
                .Else(TransitionTo(FollowUpLLM));

        ExecuteTools
            .OnEntry(ctx => Task.CompletedTask)
            .When(ToolCallCompleted)
                .If(ctx => ctx.Message.IsLastTool, then: TransitionTo(FollowUpLLM))
                .Else(TransitionTo(ExecuteTools));

        FollowUpLLM
            .OnEntry(ctx => Task.CompletedTask)
            .Finalize();

        Complete
            .Finalize();
    }
}
```

### Step 4: Enable saga registration in `Program.cs`
Replace commented-out saga registration:

```csharp
// In AddMassTransit:
cfg.AddSagaDbContext<SagaDbContext>();
cfg.AddSagaStateMachine<AgentSaga, SagaDbContext>()
   .EntityFrameworkRepository(r =>
   {
       r.ConcurrencyMode = ConcurrencyMode.Optimistic;
       r.AddDbContext<DbContext, SagaDbContext>((provider, builder) =>
       {
           builder.UseNpgsql(connectionString, m =>
           {
               m.MigrationsAssembly(typeof(AppDbContext).Assembly.GetName().Name);
               m.MigrationsHistoryTable($"__{nameof(SagaDbContext)}");
           });
       });
       r.UsePostgres();
   });

// In RabbitMQ config:
cfg2.ReceiveEndpoint("saga.agent", ep =>
{
    ep.Saga<AgentSaga>(context);
    ep.PrefetchCount = 10;
    ep.UseMessageRetry(retry => retry.Interval(3, TimeSpan.FromMilliseconds(500)));
});
```

### Step 5: Create migration for saga table
```bash
dotnet ef migrations add AgentSagaPersistence
```

### Step 6: Build & verify
```bash
dotnet build
```

---

## After Session 2 (Future)
- **Phase 3: Remove GameAgent.cs** — `IGameAgentManager` still used by `GameLifecycleHandler` and `EventBusWorker`. Will need to:
  1. Move startup recovery logic into a MassTransit consumer or background service
  2. Remove `IGameAgentManager` interface and `GameAgentManager` class
  3. Update `GameLifecycleHandler` to use saga/event-based recovery instead of `IGameAgentManager`

---

## Risk Checklist
- [ ] Saga events need saga-specific correlation (SagaId → CorrelationId)
- [ ] Handler constructor signatures change (add IPublishEndpoint) ✅ Done
- [ ] GameAgent removal may break IGameAgent interface — need to check callers
- [ ] Saga registration order (must be after consumers)
- [ ] Parallel run testing (verify no duplicate events during transition)
- [ ] DLQ consumer for failed events (manual inspection)

## Notes
- MassTransit 8.3.6 is MIT licensed (9.x became commercial-only)
- RabbitMQ.Client 7.0 removed — MassTransit.RabbitMQ provides its own
- Saga persistence survives container restarts via EF Core
- AgentBus already handles saga state in DB — saga just adds crash recovery for multi-step flows
