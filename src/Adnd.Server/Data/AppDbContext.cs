using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Adnd.Server.Models;

namespace Adnd.Server.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Game> Games => Set<Game>();
    public DbSet<Player> Players => Set<Player>();
    public DbSet<Character> Characters => Set<Character>();
    public DbSet<GameSession> GameSessions => Set<GameSession>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<NPC> NPCs => Set<NPC>();
    public DbSet<PlotThread> PlotThreads => Set<PlotThread>();
    public DbSet<PlotReview> PlotReviews => Set<PlotReview>();
    public DbSet<CustomSystemDefinition> CustomSystems => Set<CustomSystemDefinition>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AgentCall> AgentCalls => Set<AgentCall>();
    public DbSet<ToolCallCoordinator> ToolCallCoordinators => Set<ToolCallCoordinator>();
    public DbSet<GMToolCall> GMToolCalls => Set<GMToolCall>();
    public DbSet<Whisper> Whispers => Set<Whisper>();
    public DbSet<LLMPreset> LLMPresets => Set<LLMPreset>();
    public DbSet<LLMInteractionLog> LLMInteractionLogs => Set<LLMInteractionLog>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Combat> Combats => Set<Combat>();
    public DbSet<CombatParticipant> CombatParticipants => Set<CombatParticipant>();
    public DbSet<CombatEvent> CombatEvents => Set<CombatEvent>();
    public DbSet<SessionNote> SessionNotes => Set<SessionNote>();
    public DbSet<PromptTemplate> PromptTemplates => Set<PromptTemplate>();
    public DbSet<GameTemplate> GameTemplates => Set<GameTemplate>();
    public DbSet<EventRecord> EventRecords => Set<EventRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var jsonComparer = new ValueComparer<JsonElement>(
            (a, b) => (a.ValueKind == JsonValueKind.Undefined ? "null" : a.GetRawText()) ==
                      (b.ValueKind == JsonValueKind.Undefined ? "null" : b.GetRawText()),
            v => v.ValueKind == JsonValueKind.Undefined ? 0 : v.GetRawText().GetHashCode(),
            v => v.ValueKind == JsonValueKind.Undefined ? default : JsonSerializer.Deserialize<JsonElement>(v.GetRawText()));

        var stringListComparer = new ValueComparer<List<string>>(
            (a, b) => a != null && b != null && a.SequenceEqual(b),
            v => v.Aggregate(0, (h, s) => HashCode.Combine(h, s.GetHashCode())),
            v => v.ToList());

        // MilestoneEvent is a class, so a plain SequenceEqual would compare by reference and
        // a Count-only comparison misses in-place edits entirely — flipping a milestone's
        // Status used to be invisible to the change tracker and was never persisted.
        // Compare and snapshot by serialized content instead.
        var milestoneListComparer = new ValueComparer<List<MilestoneEvent>>(
            (a, b) => JsonSerializer.Serialize(a, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(b, (JsonSerializerOptions?)null),
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null).GetHashCode(),
            v => JsonSerializer.Deserialize<List<MilestoneEvent>>(JsonSerializer.Serialize(v, (JsonSerializerOptions?)null)) ?? new List<MilestoneEvent>());

        // AdvanceStep appends to this list in place, so the same content-comparer approach
        // as milestoneListComparer applies here too.
        var stepEventListComparer = new ValueComparer<List<AgentStepEvent>>(
            (a, b) => JsonSerializer.Serialize(a, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(b, (JsonSerializerOptions?)null),
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null).GetHashCode(),
            v => JsonSerializer.Deserialize<List<AgentStepEvent>>(JsonSerializer.Serialize(v, (JsonSerializerOptions?)null)) ?? new List<AgentStepEvent>());

        // ── Soft-delete global filters ──
        modelBuilder.Entity<Game>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Player>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Message>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Character>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<NPC>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<PlotThread>().HasQueryFilter(e => !e.IsDeleted);

        // ── User ──
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email).IsUnique();

        // ── RefreshToken ──
        modelBuilder.Entity<RefreshToken>()
            .HasIndex(r => r.Token).IsUnique();

        // ── LLMPreset ──
        modelBuilder.Entity<LLMPreset>()
            .HasIndex(p => new { p.UserId, p.Name }).IsUnique();
        modelBuilder.Entity<LLMPreset>()
            .Property(p => p.ExtraParams).HasColumnType("jsonb")
            .HasConversion(
                v => v.ValueKind == JsonValueKind.Undefined ? "{}" : v.GetRawText(),
                v => string.IsNullOrEmpty(v) ? JsonDocument.Parse("{}").RootElement : JsonSerializer.Deserialize<JsonElement>(v))
            .Metadata.SetValueComparer(jsonComparer);
        modelBuilder.Entity<LLMPreset>()
            .HasOne(p => p.User)
            .WithMany(u => u.LLMPresets)
            .OnDelete(DeleteBehavior.Cascade);

        // ── Game ──
        modelBuilder.Entity<Game>()
            .Property(g => g.InviteCode).HasColumnName("invitecode");
        modelBuilder.Entity<Game>()
            .HasIndex(g => g.InviteCode)
            .IsUnique()
            .HasFilter("\"invitecode\" IS NOT NULL");
        modelBuilder.Entity<Game>()
            .HasOne(g => g.LLMPreset)
            .WithMany(p => p.Games)
            .HasForeignKey(g => g.LLMPresetId)
            .OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<Game>()
            .HasOne(g => g.CurrentSession)
            .WithOne()
            .HasForeignKey<Game>(g => g.CurrentSessionId)
            .OnDelete(DeleteBehavior.SetNull);

        // ── Player ──
        modelBuilder.Entity<Player>()
            .HasIndex(p => new { p.GameId, p.UserId }).IsUnique();

        // ── Character ──
        modelBuilder.Entity<Character>()
            .HasOne(c => c.Player)
            .WithOne(p => p.Character)
            .HasForeignKey<Character>(c => c.PlayerId);

        ConfigureJsonb<Character>(modelBuilder, c => c.Attributes, jsonComparer);
        ConfigureJsonb<Character>(modelBuilder, c => c.Skills, jsonComparer);
        ConfigureJsonb<Character>(modelBuilder, c => c.Inventory, jsonComparer);
        ConfigureJsonb<Character>(modelBuilder, c => c.Spells, jsonComparer);
        ConfigureJsonb<Character>(modelBuilder, c => c.Conditions, jsonComparer);
        ConfigureJsonb<Character>(modelBuilder, c => c.CustomFields, jsonComparer);
        ConfigureJsonb<Character>(modelBuilder, c => c.SpellSlots, jsonComparer);
        ConfigureJsonb<Character>(modelBuilder, c => c.Features, jsonComparer);

        // ── Message ──
        modelBuilder.Entity<Message>()
            .HasIndex(m => new { m.SessionId, m.CreatedAt });
        modelBuilder.Entity<Message>()
            .Property(m => m.Embedding).HasColumnType("vector");
        ConfigureJsonb<Message>(modelBuilder, m => m.Metadata, jsonComparer);

        // ── NPC ──
        ConfigureJsonb<NPC>(modelBuilder, n => n.Attributes, jsonComparer);
        ConfigureJsonb<NPC>(modelBuilder, n => n.Skills, jsonComparer);
        ConfigureJsonb<NPC>(modelBuilder, n => n.Inventory, jsonComparer);

        // ── PlotThread ──
        modelBuilder.Entity<PlotThread>()
            .Property(p => p.Embedding).HasColumnType("vector");
        modelBuilder.Entity<PlotThread>()
            .Property(p => p.AdaptationHistory)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v) ?? new())
            .Metadata.SetValueComparer(stringListComparer);
        modelBuilder.Entity<PlotThread>()
            .Property(p => p.MilestoneEvents)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<MilestoneEvent>>(v) ?? new())
            .Metadata.SetValueComparer(milestoneListComparer);

        // ── PlotReview ──
        ConfigureJsonb<PlotReview>(modelBuilder, p => p.Updates, jsonComparer);

        // ── AgentCall ──
        modelBuilder.Entity<AgentCall>()
            .HasIndex(a => new { a.GameId, a.Status, a.CreatedAt });
        modelBuilder.Entity<AgentCall>()
            .HasOne(a => a.ParentCall)
            .WithMany(a => a.ChildCalls)
            .HasForeignKey(a => a.ParentCallId)
            .OnDelete(DeleteBehavior.Restrict);
        ConfigureJsonb<AgentCall>(modelBuilder, a => a.Metadata, jsonComparer);
        modelBuilder.Entity<AgentCall>()
            .Property(a => a.StepHistory)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<AgentStepEvent>>(v) ?? new())
            .Metadata.SetValueComparer(stepEventListComparer);

        // ── GMToolCall ──
        ConfigureJsonb<GMToolCall>(modelBuilder, g => g.Arguments, jsonComparer);
        ConfigureJsonb<GMToolCall>(modelBuilder, g => g.Result, jsonComparer);

        // ── ToolCallCoordinator ──
        ConfigureJsonb<ToolCallCoordinator>(modelBuilder, t => t.ToolResults, jsonComparer);
        ConfigureJsonb<ToolCallCoordinator>(modelBuilder, t => t.ToolCalls, jsonComparer);

        // ── Combat ──
        ConfigureJsonb<Combat>(modelBuilder, c => c.Notes, jsonComparer);

        // ── CombatParticipant ──
        modelBuilder.Entity<CombatParticipant>()
            .HasOne(cp => cp.Combat)
            .WithMany(c => c.Participants)
            .HasForeignKey(cp => cp.CombatId)
            .OnDelete(DeleteBehavior.Cascade);
        ConfigureJsonb<CombatParticipant>(modelBuilder, cp => cp.Conditions, jsonComparer);
        ConfigureJsonb<CombatParticipant>(modelBuilder, cp => cp.TemporaryHP, jsonComparer);
        ConfigureJsonb<CombatParticipant>(modelBuilder, cp => cp.SavingThrows, jsonComparer);
        ConfigureJsonb<CombatParticipant>(modelBuilder, cp => cp.DeathSaveState, jsonComparer);

        // ── CombatEvent ──
        modelBuilder.Entity<CombatEvent>()
            .HasOne(ce => ce.Combat)
            .WithMany(c => c.Events)
            .HasForeignKey(ce => ce.CombatId)
            .OnDelete(DeleteBehavior.Cascade);
        ConfigureJsonb<CombatEvent>(modelBuilder, ce => ce.Metadata, jsonComparer);

        // ── LLMInteractionLog ──
        modelBuilder.Entity<LLMInteractionLog>()
            .HasIndex(l => new { l.UserId, l.OriginGameId, l.StartedAt });

        // ── Whisper ──
        ConfigureJsonb<Whisper>(modelBuilder, w => w.TargetPlayerIds, jsonComparer);

        // ── AuditLog ──
        ConfigureJsonb<AuditLog>(modelBuilder, a => a.Details, jsonComparer);

        // ── EventRecord ──
        modelBuilder.Entity<EventRecord>()
            .HasIndex(e => new { e.GameId, e.Status });
        modelBuilder.Entity<EventRecord>()
            .HasIndex(e => e.CorrelationId).IsUnique();
        ConfigureJsonb<EventRecord>(modelBuilder, e => e.Payload, jsonComparer);

        // ── CustomSystemDefinition ──
        ConfigureJsonb<CustomSystemDefinition>(modelBuilder, c => c.Definition, jsonComparer);
    }

    private static void ConfigureJsonb<T>(
        ModelBuilder modelBuilder,
        System.Linq.Expressions.Expression<Func<T, JsonElement>> prop,
        ValueComparer<JsonElement> comparer)
        where T : class
    {
        modelBuilder.Entity<T>()
            .Property(prop)
            .HasColumnType("jsonb")
            .HasConversion(
                v => v.ValueKind == JsonValueKind.Undefined ? "null" : v.GetRawText(),
                v => string.IsNullOrEmpty(v) ? default : JsonSerializer.Deserialize<JsonElement>(v))
            .Metadata.SetValueComparer(comparer);
    }
}
