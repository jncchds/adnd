# Event Bus Replacement (MediatR → RabbitMQ) Implementation Plan

> **REQUIRED SUB-SKILL:** Use the executing-plans skill to implement this plan task-by-task.

**Goal:** Replace MediatR with a RabbitMQ-backed event bus while preserving all existing functionality.

**Architecture:** Custom `IEventBus` with per-game RabbitMQ queues, `EventRecord` table for durability, startup reflection scan → O(1) dictionary dispatch, `EventBusWorker` background service for delivery, GameAgent subscribes to `agent.{GameId}` queue for reactive wakeup.

**Tech Stack:** RabbitMQ.Client 7.x, .NET 10, EF Core, ASP.NET Core, docker-compose

---

## Task 1: Add RabbitMQ Infrastructure

**Files:**
- Modify: `docker-compose.yml` — add RabbitMQ service
- Create: `src/Adnd.Server/Services/RabbitMqEventBus.cs`
- Create: `src/Adnd.Server/Services/EventBusWorker.cs`
- Create: `src/Adnd.Server/Services/IEventBus.cs`
- Create: `src/Adnd.Server/Events/IGameEvent.cs`
- Create: `src/Adnd.Server/Events/IEventHandler.cs`
- Create: `src/Adnd.Server/Models/EventRecord.cs`
- Create: `src/Adnd.Server/Models/EventStatus.cs`
- Modify: `src/Adnd.Server/Data/AppDbContext.cs` — add `DbSet<EventRecord>`
- Modify: `src/Adnd.Server/Adnd.Server.csproj` — add RabbitMQ.Client package
- Create: `docker-compose.prod.yml` — separate prod config (no 5672 exposed)

**Step 1: Add RabbitMQ to docker-compose.yml**

Add to `docker-compose.yml` services section:

```yaml
  rabbitmq:
    image: rabbitmq:3-management
    container_name: adnd-rabbitmq
    networks:
      - adnd-network
    environment:
      RABBITMQ_DEFAULT_USER: adnd
      RABBITMQ_DEFAULT_PASS: adnd
    volumes:
      - rabbitmq_data:/var/lib/rabbitmq
    ports:
      - "15672:15672"
      - "5672:5672"
    deploy:
      resources:
        limits:
          memory: 256M
```

Add to volumes section:
```yaml
  rabbitmq_data:
```

**Step 2: Create IGameEvent interface**

```csharp
// src/Adnd.Server/Events/IGameEvent.cs
namespace Adnd.Server.Events;

public interface IGameEvent
{
    Guid GameId { get; }
}
```

**Step 3: Create IEventHandler interface**

```csharp
// src/Adnd.Server/Events/IEventHandler.cs
namespace Adnd.Server.Events;

public interface IEventHandler<TEvent> where TEvent : IGameEvent
{
    Task HandleAsync(TEvent evt, CancellationToken ct = default);
}
```

**Step 4: Create EventStatus enum**

```csharp
// src/Adnd.Server/Models/EventStatus.cs
namespace Adnd.Server.Models;

public enum EventStatus
{
    Pending,
    Published,
    Acknowledged,
    Failed
}
```

**Step 5: Create EventRecord model**

```csharp
// src/Adnd.Server/Models/EventRecord.cs
namespace Adnd.Server.Models;

public class EventRecord
{
    public Guid Id { get; set; }
    public Guid GameId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public EventStatus Status { get; set; }
    public string? CorrelationId { get; set; }
    public int RetryCount { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime? AckedAt { get; set; }
    public string? Error { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

**Step 6: Create IEventBus interface**

```csharp
// src/Adnd.Server/Services/IEventBus.cs
namespace Adnd.Server.Services;

public interface IEventBus
{
    Task PublishAsync<TEvent>(TEvent evt, CancellationToken ct = default) where TEvent : IGameEvent;
    void Subscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : IGameEvent;
    void Unsubscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : IGameEvent;
}
```

**Step 7: Add RabbitMQ.Client to csproj**

```xml
<!-- src/Adnd.Server/Adnd.Server.csproj — add to ItemGroup -->
<PackageReference Include="RabbitMQ.Client" Version="7.0.0" />
```

**Step 8: Add EventRecord to AppDbContext**

```csharp
// In AppDbContext.cs, add:
public DbSet<EventRecord> EventRecords => Set<EventRecord>();
```

**Step 9: Create docker-compose.prod.yml**

```yaml
# docker-compose.prod.yml
version: '3.8'
services:
  app:
    environment:
      - RabbitMq__Host=rabbitmq
      - RabbitMq__Port=5672
      - RabbitMq__Username=adnd
      - RabbitMq__Password=adnd
      - RabbitMq__VirtualHost=/adnd

  rabbitmq:
    image: rabbitmq:3-management
    networks:
      - adnd-network
    environment:
      RABBITMQ_DEFAULT_USER: adnd
      RABBITMQ_DEFAULT_PASS: adnd
    volumes:
      - rabbitmq_data:/var/lib/rabbitmq
    ports:
      - "15672:15672"
    deploy:
      resources:
        limits:
          memory: 256M
```

**Step 10: Commit**

```bash
git add docker-compose.yml docker-compose.prod.yml src/Adnd.Server/Adnd.Server.csproj
git add src/Adnd.Server/Events/IGameEvent.cs src/Adnd.Server/Events/IEventHandler.cs
git add src/Adnd.Server/Models/EventRecord.cs src/Adnd.Server/Models/EventStatus.cs
git add src/Adnd.Server/Services/IEventBus.cs
git commit -m "feat: add RabbitMQ infrastructure and event bus interfaces"
```

---

## Task 2: Create EventRecord Migration

**Files:**
- Run: `dotnet ef migrations add EventBusReplacement --project src/Adnd.Server --startup-project src/Adnd.Server`
- Verify: `src/Adnd.Server/Data/Migrations/` — new migration file

**Step 1: Create migration**

```bash
cd src/Adnd.Server
dotnet ef migrations add EventBusReplacement --startup-project .
```

**Step 2: Verify migration content**

The migration should contain:
- `CreateTable("EventRecords", ...)` with columns: Id, GameId, EventType, Payload, Status, CorrelationId, RetryCount, PublishedAt, AckedAt, Error, CreatedAt
- Indexes: `(GameId, Status)`, `(Status, CreatedAt)`, `(CorrelationId)`

**Step 3: Commit**

```bash
git add src/Adnd.Server/Data/Migrations/
git commit -m "feat: add EventRecords table migration"
```

---

## Task 3: Implement EventBusWorker (Background Service)

**Files:**
- Create: `src/Adnd.Server/Services/EventBusWorker.cs`

**Step 1: Create EventBusWorker**

```csharp
// src/Adnd.Server/Services/EventBusWorker.cs
using Adnd.Server.Data;
using Adnd.Server.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text.Json;

namespace Adnd.Server.Services;

public class EventBusWorker : BackgroundService
{
    private readonly AppDbContext _context;
    private readonly ILogger<EventBusWorker> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly Dictionary<string, List<(string handlerId, Type eventType, Type handlerType)>> _handlerMap;
    private IConnection? _rabbitMqConnection;
    private IModel? _channel;
    private readonly object _lock = new();

    public EventBusWorker(
        AppDbContext context,
        ILogger<EventBusWorker> logger,
        IServiceProvider serviceProvider,
        IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _handlerMap = new();
    }

    public override async Task StartAsync(CancellationToken ct)
    {
        await base.StartAsync(ct);
        
        // Register handlers via reflection
        RegisterHandlers();
        
        // Connect to RabbitMQ
        ConnectToRabbitMq();
        
        // Replay pending events
        await ReplayPendingEvents(ct);
        
        _logger.LogInformation("EventBusWorker started");
    }

    private void RegisterHandlers()
    {
        var assembly = typeof(Adnd.Server.Events.GameStarted).Assembly;
        
        foreach (var type in assembly.GetTypes())
        {
            var interfaces = type.GetInterfaces()
                .Where(i => i.IsGenericType && 
                           i.GetGenericTypeDefinition() == typeof(IEventHandler<>));
            
            foreach (var iface in interfaces)
            {
                var eventType = iface.GetGenericArguments()[0];
                var key = eventType.FullName!;
                
                var handlerId = Guid.NewGuid().ToString();
                _handlerMap.GetOrAdd(key, _ => new())
                    .Add((handlerId, eventType, type));
            }
        }
        
        _logger.LogInformation("Registered {Count} event handlers", _handlerMap.Count);
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
        
        // Declare exchange
        _channel.ExchangeDeclare("adnd.events", ExchangeType.Direct, durable: true);
        
        _logger.LogInformation("Connected to RabbitMQ at {Host}:{Port}", host, port);
    }

    private async Task ReplayPendingEvents(CancellationToken ct)
    {
        var pending = await _context.EventRecords
            .Where(e => e.Status == EventStatus.Pending)
            .ToListAsync(ct);

        _logger.LogInformation("Replaying {Count} pending events from previous run", pending.Count);

        foreach (var record in pending)
        {
            await DispatchEvent(record, ct);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var pending = await _context.EventRecords
                    .Where(e => e.Status is EventStatus.Pending or EventStatus.Failed)
                    .OrderBy(e => e.CreatedAt)
                    .Take(100)
                    .ToListAsync(stoppingToken);

                foreach (var record in pending)
                {
                    await DispatchEvent(record, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in EventBusWorker poll loop");
            }

            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }

    private async Task DispatchEvent(EventRecord record, CancellationToken ct)
    {
        lock (_lock)
        {
            if (_channel == null || !_rabbitMqConnection?.IsOpen == true)
            {
                ConnectToRabbitMq();
            }
        }

        // Publish to RabbitMQ
        var body = System.Text.Encoding.UTF8.GetBytes(record.Payload);
        var properties = _channel!.CreateBasicProperties();
        properties.Persistent = true;
        properties.CorrelationId = record.CorrelationId;
        properties.DeliveryMode = 2; // persistent

        _channel.BasicPublish(
            exchange: "adnd.events",
            routingKey: $"game.{record.GameId}",
            basicProperties: properties,
            body: body);

        record.Status = EventStatus.Published;
        record.PublishedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("[EVENT] Published | GameId={GameId} | EventType={EventType} | CallId={CallId}",
            record.GameId, record.EventType, record.Id);
    }

    public override async Task StopAsync(CancellationToken ct)
    {
        _channel?.Close();
        _rabbitMqConnection?.Close();
        await base.StopAsync(ct);
    }
}
```

**Step 2: Register in Program.cs**

```csharp
// In Program.cs, add after other service registrations:
builder.Services.AddHostedService<EventBusWorker>();
```

**Step 3: Commit**

```bash
git add src/Adnd.Server/Services/EventBusWorker.cs src/Adnd.Server/Program.cs
git commit -m "feat: implement EventBusWorker with RabbitMQ connection and handler registration"
```

---

## Task 4: Implement RabbitMqEventBus (IEventBus)

**Files:**
- Create: `src/Adnd.Server/Services/RabbitMqEventBus.cs`

**Step 1: Create RabbitMqEventBus**

```csharp
// src/Adnd.Server/Services/RabbitMqEventBus.cs
using Adnd.Server.Data;
using Adnd.Server.Models;
using Adnd.Server.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using System.Text.Json;

namespace Adnd.Server.Services;

public class RabbitMqEventBus : IEventBus
{
    private readonly AppDbContext _context;
    private readonly ILogger<RabbitMqEventBus> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly Dictionary<string, List<(string handlerId, Type eventType, Type handlerType)>> _handlerMap;
    private readonly ConcurrentDictionary<string, IModel> _channelsByGame = new();
    private readonly object _lock = new();

    public RabbitMqEventBus(
        AppDbContext context,
        ILogger<RabbitMqEventBus> logger,
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        Dictionary<string, List<(string, Type, Type)>> handlerMap)
    {
        _context = context;
        _logger = logger;
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _handlerMap = handlerMap;
    }

    public async Task PublishAsync<TEvent>(TEvent evt, CancellationToken ct = default) where TEvent : IGameEvent
    {
        // Create event record
        var record = new EventRecord
        {
            Id = Guid.NewGuid(),
            GameId = evt.GameId,
            EventType = typeof(TEvent).FullName!,
            Payload = JsonSerializer.Serialize(evt),
            Status = EventStatus.Pending,
            CorrelationId = Guid.NewGuid().ToString(),
            CreatedAt = DateTime.UtcNow
        };

        _context.EventRecords.Add(record);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("[EVENT] Queued | GameId={GameId} | EventType={EventType} | CallId={CallId}",
            evt.GameId, typeof(TEvent).Name, record.Id);
    }

    public void Subscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : IGameEvent
    {
        // Dynamic subscription — not used in current design (startup scan handles it)
        // Kept for future extensibility
    }

    public void Unsubscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : IGameEvent
    {
        // Dynamic unsubscription
    }

    private IModel GetOrCreateChannel(Guid gameId)
    {
        return _channelsByGame.GetOrAdd(gameId.ToString(), _ =>
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

            var connection = factory.CreateConnection();
            var channel = connection.CreateModel();
            
            // Declare game queue
            var queueName = $"game.{gameId}";
            channel.QueueDeclare(queueName, durable: true, exclusive: false, autoDelete: false, null);
            channel.QueueBind(queueName, "adnd.events", $"game.{gameId}");
            
            // Declare DLQ
            var dlqName = $"dlq.game.{gameId}";
            channel.QueueDeclare(dlqName, durable: true, exclusive: false, autoDelete: false, null);
            channel.QueueBind(dlqName, "adnd.events", $"dlq.game.{gameId}");
            
            // Set dead-letter exchange on main queue
            channel.QueueDeclare(queueName, durable: true, exclusive: false, autoDelete: false, 
                new Dictionary<string, object> { ["x-dead-letter-exchange"] = "adnd.events", ["x-dead-letter-routing-key"] = $"dlq.game.{gameId}" });

            return channel;
        });
    }
}
```

**Step 2: Register in Program.cs**

```csharp
// In Program.cs, add:
builder.Services.AddSingleton<RabbitMqEventBus>();
builder.Services.AddSingleton<IEventBus>(sp => sp.GetRequiredService<RabbitMqEventBus>());
```

**Step 3: Commit**

```bash
git add src/Adnd.Server/Services/RabbitMqEventBus.cs src/Adnd.Server/Program.cs
git commit -m "feat: implement RabbitMqEventBus with per-game queue management"
```

---

## Task 5: Migrate Event Types (Remove INotification, Add IGameEvent)

**Files:**
- Modify: `src/Adnd.Server/Events/GameEvents.cs` — change all event types

**Step 1: Update all event types**

In `GameEvents.cs`, change every event from:
```csharp
public record GameCreated(Guid GameId, ...) : INotification;
```
to:
```csharp
public record GameCreated(Guid GameId, ...) : IGameEvent;
```

**Step 2: Verify**

```bash
grep -c ": INotification" src/Adnd.Server/Events/GameEvents.cs  # Should be 0
grep -c ": IGameEvent" src/Adnd.Server/Events/GameEvents.cs    # Should be > 0
```

**Step 3: Commit**

```bash
git add src/Adnd.Server/Events/GameEvents.cs
git commit -m "refactor: replace INotification with IGameEvent on all event types"
```

---

## Task 6: Migrate Handlers to IEventHandler<>

**Files:**
- Modify: `src/Adnd.Server/Handlers/GameEventHandlers.cs`
- Modify: `src/Adnd.Server/Handlers/PlotWeaverHandler.cs`
- Modify: `src/Adnd.Server/Handlers/AgentCallQueuedHandler.cs`

**Step 1: Migrate GameLifecycleHandler**

```csharp
// Before:
public class GameLifecycleHandler :
    INotificationHandler<GameCreated>,
    INotificationHandler<GameStarted>,
    ...

// After:
public class GameLifecycleHandler :
    IEventHandler<GameCreated>,
    IEventHandler<GameStarted>,
    IEventHandler<GameArchived>,
    IEventHandler<GamePaused>,
    IEventHandler<GameResumed>,
    IEventHandler<GameNarrationStarted>
{
    // ... constructor unchanged ...

    // Rename Handle → HandleAsync, add CancellationToken parameter
    public async Task HandleAsync(GameCreated evt, CancellationToken ct = default)
    {
        // ... same body ...
    }

    public async Task HandleAsync(GameStarted evt, CancellationToken ct = default)
    {
        // ... same body ...
    }

    // ... etc ...
}
```

**Step 2: Migrate PlayerHandler**

```csharp
public class PlayerHandler :
    IEventHandler<PlayerJoined>,
    IEventHandler<PlayerLeft>
{
    // Rename Handle → HandleAsync
    public Task HandleAsync(PlayerJoined evt, CancellationToken ct = default)
    {
        _logger.LogInformation("[PLAYER] Joined | ...");
        return Task.CompletedTask;
    }

    public Task HandleAsync(PlayerLeft evt, CancellationToken ct = default)
    {
        _logger.LogInformation("[PLAYER] Left | ...");
        return Task.CompletedTask;
    }
}
```

**Step 3: Migrate GameActionHandler**

```csharp
public class GameActionHandler :
    IEventHandler<SkillCheckRequested>,
    IEventHandler<AttackRequested>,
    IEventHandler<CombatStarted>,
    IEventHandler<CombatEnded>,
    IEventHandler<StorySwayed>,
    IEventHandler<CombatAttackExecuted>,
    IEventHandler<CombatSaveThrowExecuted>,
    IEventHandler<CombatSpellCast>,
    IEventHandler<CombatDamageDealt>,
    IEventHandler<CombatHealed>,
    IEventHandler<CombatXPGranted>,
    IEventHandler<CombatLevelUp>,
    IEventHandler<CombatRestStarted>,
    IEventHandler<CombatRestEnded>
{
    // Rename Handle → HandleAsync
    public async Task HandleAsync(SkillCheckRequested evt, CancellationToken ct = default)
    {
        await QueueGMMaybe(evt.GameId, evt.SessionId, $"Skill check: {evt.Skill} (DC {evt.DC}) by player {evt.PlayerId}");
    }

    // ... etc ...
}
```

**Step 4: Migrate ChatHandler**

```csharp
public class ChatHandler :
    IEventHandler<MessageSent>,
    IEventHandler<WhisperSent>,
    IEventHandler<OOCMessageSent>,
    IEventHandler<OOCWhisperSent>,
    IEventHandler<OOCWhisperReceived>
{
    // Rename Handle → HandleAsync
    public Task HandleAsync(MessageSent evt, CancellationToken ct = default)
    {
        _logger.LogInformation("[CHAT] MessageSent | ...");
        return Task.CompletedTask;
    }

    // ... etc ...
}
```

**Step 5: Migrate SessionHandler**

```csharp
public class SessionHandler :
    IEventHandler<SessionCreated>,
    IEventHandler<SessionClosed>
{
    // Rename Handle → HandleAsync
    public Task HandleAsync(SessionCreated evt, CancellationToken ct = default)
    {
        _logger.LogInformation("[SESSION] Created | ...");
        return Task.CompletedTask;
    }

    public Task HandleAsync(SessionClosed evt, CancellationToken ct = default)
    {
        _logger.LogInformation("[SESSION] Closed | ...");
        return Task.CompletedTask;
    }
}
```

**Step 6: Migrate PlotWeaverHandler**

```csharp
public class PlotWeaverHandler :
    IEventHandler<GameStarted>,
    IEventHandler<CombatEnded>,
    IEventHandler<CombatStarted>,
    IEventHandler<NPCCreated>,
    IEventHandler<NPCDeleted>,
    IEventHandler<NPCUpdated>,
    IEventHandler<CharacterUpdated>,
    IEventHandler<StorySwayed>,
    IEventHandler<PlayerJoined>,
    IEventHandler<MessageSent>,
    IEventHandler<PlotThreadCreated>,
    IEventHandler<PlotThreadUpdated>
{
    // Rename Handle → HandleAsync
    public async Task HandleAsync(GameStarted evt, CancellationToken ct = default)
    {
        // ... same body ...
    }

    // ... etc ...
}
```

**Step 7: Remove AgentCallQueuedHandler**

The `AgentCallQueued` event wakes up the GameAgent. This is replaced by:
1. GameAgent subscribing to `agent.{GameId}` RabbitMQ queue
2. `AgentBus.SendCallAsync` publishing to that queue

**Delete:** `src/Adnd.Server/Handlers/AgentCallQueuedHandler.cs`

**Modify:** `src/Adnd.Server/Services/AgentBus.cs` — replace MediatR publish with RabbitMQ publish

```csharp
// In AgentBus.SendCallAsync, replace:
await _mediator.Publish(new Events.AgentCallQueued(call.GameId, call.Id));

// With:
await _eventBus.PublishAsync(new AgentCallQueued(call.GameId, call.Id));
```

**Step 8: Commit**

```bash
git add src/Adnd.Server/Handlers/GameEventHandlers.cs
git add src/Adnd.Server/Handlers/PlotWeaverHandler.cs
git rm src/Adnd.Server/Handlers/AgentCallQueuedHandler.cs
git commit -m "refactor: migrate all handlers to IEventHandler<> interface"
```

---

## Task 7: Update Controllers/Hubs to Use IEventBus

**Files:**
- Modify: `src/Adnd.Server/Hubs/GameHub.cs`
- Modify: `src/Adnd.Server/Controllers/GamesController.cs`
- Modify: `src/Adnd.Server/Controllers/Games.cs`
- Modify: `src/Adnd.Server/Controllers/GameState.cs`
- Modify: `src/Adnd.Server/Controllers/GMStatus.cs`
- Modify: `src/Adnd.Server/Services/IGameStart.cs`
- Modify: `src/Adnd.Server/Hubs/GameHub.ChatMethods.cs`
- Modify: `src/Adnd.Server/Hubs/GameHub.Dice.cs`
- Modify: `src/Adnd.Server/Services/AgentBus.cs`

**Step 1: Replace IMediator with IEventBus in all files**

In each file, find:
```csharp
private readonly IMediator _mediator;
```
Replace with:
```csharp
private readonly IEventBus _eventBus;
```

Find all usages of:
```csharp
await _mediator.Publish(new SomeEvent(...));
```
Replace with:
```csharp
await _eventBus.PublishAsync(new SomeEvent(...));
```

**Step 2: Update constructor injection**

```csharp
// Before:
public SomeController(IMediator mediator) { _mediator = mediator; }

// After:
public SomeController(IEventBus eventBus) { _eventBus = eventBus; }
```

**Step 3: Commit**

```bash
git add src/Adnd.Server/Hubs/GameHub.cs
git add src/Adnd.Server/Controllers/GamesController.cs
git add src/Adnd.Server/Controllers/Games.cs
git add src/Adnd.Server/Controllers/GameState.cs
git add src/Adnd.Server/Controllers/GMStatus.cs
git add src/Adnd.Server/Services/IGameStart.cs
git add src/Adnd.Server/Hubs/GameHub.ChatMethods.cs
git add src/Adnd.Server/Hubs/GameHub.Dice.cs
git add src/Adnd.Server/Services/AgentBus.cs
git commit -m "refactor: replace IMediator with IEventBus in all controllers and hubs"
```

---

## Task 8: Remove MediatR Dependency

**Files:**
- Modify: `src/Adnd.Server/Adnd.Server.csproj` — remove MediatR package
- Modify: `src/Adnd.Server/Program.cs` — remove MediatR registration
- Modify: `src/Adnd.Server/Events/GameEvents.cs` — remove `using MediatR`
- Modify: `src/Adnd.Server/Services/AgentBus.cs` — remove `using MediatR`

**Step 1: Remove MediatR package**

```xml
<!-- In Adnd.Server.csproj, remove: -->
<PackageReference Include="MediatR" Version="14.1.0" />
```

**Step 2: Remove MediatR registration from Program.cs**

```csharp
// Remove:
using MediatR;

// Remove:
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(GameLifecycleHandler).Assembly);
});
```

**Step 3: Remove MediatR usings from all files**

```bash
# Remove "using MediatR;" from:
# src/Adnd.Server/Events/GameEvents.cs
# src/Adnd.Server/Services/AgentBus.cs
# Any other files that still have it
```

**Step 4: Commit**

```bash
git add src/Adnd.Server/Adnd.Server.csproj src/Adnd.Server/Program.cs
git add src/Adnd.Server/Events/GameEvents.cs
git commit -m "chore: remove MediatR dependency"
```

---

## Task 9: Add AgentCallQueued Event + GameAgent RabbitMQ Subscription

**Files:**
- Create: `src/Adnd.Server/Events/AgentCallQueued.cs` (if not already in GameEvents.cs)
- Modify: `src/Adnd.Server/Agent/GameAgent.cs` — add RabbitMQ subscription
- Modify: `src/Adnd.Server/Services/AgentBus.cs` — publish to agent queue

**Step 1: Ensure AgentCallQueued event exists**

If it's in `GameEvents.cs`, keep it there. If it's a separate file, ensure it implements `IGameEvent`.

**Step 2: Update AgentBus.SendCallAsync to publish to agent queue**

```csharp
public async Task<AgentCall> SendCallAsync(AgentCall call)
{
    call.Status = AgentCallStatus.Pending;
    call.CreatedAt = DateTime.UtcNow;

    _context.AgentCalls.Add(call);
    await _context.SaveChangesAsync();

    // Wake up the GameAgent via RabbitMQ (replaces MediatR publish)
    await _eventBus.PublishAsync(new AgentCallQueued(call.GameId, call.Id));

    return call;
}
```

**Step 3: Update GameAgent to subscribe to agent queue**

In `GameAgent.cs`, add RabbitMQ subscription:

```csharp
public async Task StartAsync(Guid gameId, Guid creatorId)
{
    // ... existing code ...
    
    // Subscribe to agent queue for reactive wakeup
    var queueName = $"agent.{gameId}";
    var factory = new ConnectionFactory
    {
        HostName = _configuration["RabbitMq:Host"] ?? "localhost",
        Port = _configuration.GetValue<int>("RabbitMq:Port", 5672),
        UserName = _configuration["RabbitMq:Username"] ?? "adnd",
        Password = _configuration["RabbitMq:Password"] ?? "adnd",
        VirtualHost = _configuration["RabbitMq:VirtualHost"] ?? "/adnd"
    };

    var connection = factory.CreateConnection();
    var channel = connection.CreateModel();
    channel.QueueDeclare(queueName, durable: true, exclusive: false, autoDelete: false, null);
    channel.QueueBind(queueName, "adnd.events", $"agent.{gameId}");

    var consumer = new EventingBasicConsumer(channel);
    consumer.Received += (model, ea) =>
    {
        var body = ea.Body.ToArray();
        var message = System.Text.Encoding.UTF8.GetString(body);
        // Process the AgentCallQueued event
        WakeUpAgent();
    };
    channel.BasicConsume(queueName, autoAck: false, consumer);

    _agentChannel = channel;
    _agentConnection = connection;
}
```

**Step 4: Commit**

```bash
git add src/Adnd.Server/Events/AgentCallQueued.cs
git add src/Adnd.Server/Agent/GameAgent.cs
git add src/Adnd.Server/Services/AgentBus.cs
git commit -m "feat: add reactive GameAgent wakeup via RabbitMQ subscription"
```

---

## Task 10: Add EventRecord Cleanup Service

**Files:**
- Create: `src/Adnd.Server/Services/EventRecordCleanupService.cs`

**Step 1: Create cleanup service**

```csharp
// src/Adnd.Server/Services/EventRecordCleanupService.cs
using Adnd.Server.Data;
using Adnd.Server.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Adnd.Server.Services;

public class EventRecordCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EventRecordCleanupService> _logger;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(6);
    private readonly int _retentionDays = 7;

    public EventRecordCleanupService(
        IServiceScopeFactory scopeFactory,
        ILogger<EventRecordCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupAcknowledgedEvents(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in EventRecordCleanupService");
            }

            await Task.Delay(_cleanupInterval, stoppingToken);
        }
    }

    private async Task CleanupAcknowledgedEvents(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var cutoff = DateTime.UtcNow.AddDays(-_retentionDays);
        var deleted = await context.EventRecords
            .Where(e => e.Status == EventStatus.Acknowledged && e.AckedAt < cutoff)
            .ExecuteDeleteAsync(ct);

        if (deleted > 0)
        {
            _logger.LogInformation("Cleaned up {Count} acknowledged EventRecords older than {Days} days",
                deleted, _retentionDays);
        }
    }
}
```

**Step 2: Register in Program.cs**

```csharp
builder.Services.AddHostedService<EventRecordCleanupService>();
```

**Step 3: Commit**

```bash
git add src/Adnd.Server/Services/EventRecordCleanupService.cs src/Adnd.Server/Program.cs
git commit -m "feat: add EventRecord cleanup service (7-day retention)"
```

---

## Task 11: Build Verification

**Files:**
- Run: `dotnet build src/Adnd.Server/Adnd.Server.csproj`

**Step 1: Build**

```bash
cd src/Adnd.Server
dotnet build Adnd.Server.csproj
```

**Expected:** Build succeeds with 0 errors. Warnings are acceptable (pre-existing).

**Step 2: Commit**

```bash
git add -A
git commit -m "chore: verify build after event bus migration"
```

---

## Task 12: Final Review & Testing

**Files:**
- Verify: `docker-compose up -d` starts RabbitMQ
- Verify: `dotnet run` starts without MediatR errors
- Verify: Events are published to RabbitMQ queues

**Step 1: Start infrastructure**

```bash
docker-compose up -d rabbitmq
```

**Step 2: Start app**

```bash
cd src/Adnd.Server
dotnet run
```

**Expected:** App starts, connects to RabbitMQ, registers handlers, no errors.

**Step 3: Verify queues**

Check RabbitMQ management UI at `http://localhost:15672` (guest/guest):
- `game.{GameId}` queues exist after game creation
- `dlq.game.{GameId}` queues exist for each game
- `adnd.events` exchange exists

**Step 4: Commit**

```bash
git add -A
git commit -m "chore: verify event bus replacement end-to-end"
```

---

## Risk Checklist

- [ ] RabbitMQ.Client version compatibility with .NET 10
- [ ] EventRecord table indexes (verify migration includes them)
- [ ] Handler registration order (no circular dependencies)
- [ ] GameAgent RabbitMQ connection lifecycle (reconnect on disconnect)
- [ ] DLQ worker for failed events (not in scope — can add later)
- [ ] EventRecord cleanup (7-day purge)
- [ ] docker-compose.prod.yml port configuration (5672 internal only)

## Open Questions

1. **DLQ worker:** Not implemented in this plan. Can be added later as a separate task.
2. **Metrics:** Not in scope. Can add later (events published, acked, failed).
3. **Health check:** RabbitMQ health check not added. Can add later.
4. **Testing:** Integration tests with TestContainers for RabbitMQ — not in scope for v1.
