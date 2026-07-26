using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using System.Text.Json;

namespace Adnd.Server.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

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

    // Audit
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    // Combat
    public DbSet<Combat> Combats => Set<Combat>();
    public DbSet<CombatParticipant> CombatParticipants => Set<CombatParticipant>();
    public DbSet<CombatEvent> CombatEvents => Set<CombatEvent>();

    // Session Notes
    public DbSet<SessionNote> SessionNotes => Set<SessionNote>();

    // Prompt Templates
    public DbSet<PromptTemplate> PromptTemplates => Set<PromptTemplate>();

    // Game Templates
    public DbSet<GameTemplate> GameTemplates => Set<GameTemplate>();

    // Event Records (durable event bus)
    public DbSet<EventRecord> EventRecords => Set<EventRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // PGVector for PlotThread embeddings
        modelBuilder.Entity<PlotThread>()
            .Property(p => p.Embedding)
            .HasColumnType("vector")
            .IsRequired(false);

        // PGVector for Message embeddings
        modelBuilder.Entity<Message>()
            .Property(m => m.Embedding)
            .HasColumnType("vector")
            .IsRequired(false);

        // Value converter for Message Metadata (JsonElement)
        modelBuilder.Entity<Message>()
            .Property(m => m.Metadata)
            .HasConversion(
                v => v.ValueKind == JsonValueKind.Undefined ? "{}" : v.GetRawText(),
                v => string.IsNullOrEmpty(v) || v == "{}" ? default : JsonDocument.Parse(v).RootElement,
                new ValueComparer<JsonElement>(
                    (a, b) => (a.ValueKind == JsonValueKind.Undefined && b.ValueKind == JsonValueKind.Undefined) ||
                              (a.ValueKind != JsonValueKind.Undefined && b.ValueKind != JsonValueKind.Undefined && a.GetRawText() == b.GetRawText()),
                    v => v.ValueKind == JsonValueKind.Undefined ? 0 : v.GetRawText().GetHashCode(),
                    v => v.ValueKind == JsonValueKind.Undefined ? default : JsonDocument.Parse(v.GetRawText()).RootElement
                ))
            .HasColumnType("jsonb");

        // PlotThread new fields
        modelBuilder.Entity<PlotThread>()
            .Property(p => p.Momentum)
            .HasColumnType("real");
        modelBuilder.Entity<PlotThread>()
            .Property(p => p.RelevanceScore)
            .HasColumnType("real");
        modelBuilder.Entity<PlotThread>()
            .Property(p => p.AdaptationHistory)
            .HasConversion(
                v => v == null || !v.Any() ? "[]" : JsonSerializer.Serialize(v),
                v => string.IsNullOrEmpty(v) || v == "[]" ? new List<string>() : JsonSerializer.Deserialize<List<string>>(v) ?? new List<string>())
            .HasColumnType("jsonb");
        modelBuilder.Entity<PlotThread>()
            .Property(p => p.MilestoneEvents)
            .HasConversion(
                v => v == null || !v.Any() ? "[]" : JsonSerializer.Serialize(v),
                v => string.IsNullOrEmpty(v) || v == "[]" ? new List<MilestoneEvent>() : JsonSerializer.Deserialize<List<MilestoneEvent>>(v) ?? new List<MilestoneEvent>())
            .HasColumnType("jsonb");

        // PlotReview
        modelBuilder.Entity<PlotReview>()
            .HasOne(pr => pr.Game)
            .WithMany(g => g.PlotReviews)
            .HasForeignKey(pr => pr.GameId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<PlotReview>()
            .Property(pr => pr.Updates)
            .HasConversion(
                v => v == null || !v.Any() ? "[]" : JsonSerializer.Serialize(v),
                v => string.IsNullOrEmpty(v) || v == "[]" ? new List<ThreadUpdate>() : JsonSerializer.Deserialize<List<ThreadUpdate>>(v) ?? new List<ThreadUpdate>())
            .HasColumnType("jsonb");

        modelBuilder.Entity<PlotReview>()
            .HasIndex(pr => new { pr.GameId, pr.ReviewedAt })
            .IsDescending(new[] { false, true });

        // User email unique
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        // Game invite code unique
        modelBuilder.Entity<Game>()
            .Property(g => g.InviteCode)
            .HasColumnName("invitecode");
        modelBuilder.Entity<Game>()
            .HasIndex(g => g.InviteCode)
            .IsUnique()
            .HasFilter("invitecode IS NOT NULL");

        // Game → LLM preset relationship
        modelBuilder.Entity<Game>()
            .HasOne(g => g.LLMPreset)
            .WithMany()
            .HasForeignKey(g => g.LLMPresetId)
            .OnDelete(DeleteBehavior.SetNull);

        // Game → CurrentSession (one-to-one)
        modelBuilder.Entity<Game>()
            .HasOne(g => g.CurrentSession)
            .WithOne()
            .HasForeignKey<Game>(g => g.CurrentSessionId)
            .OnDelete(DeleteBehavior.SetNull);

        // Refresh token unique
        modelBuilder.Entity<RefreshToken>()
            .HasIndex(r => r.Token)
            .IsUnique();

        // Player unique per game
        modelBuilder.Entity<Player>()
            .HasIndex(p => new { p.GameId, p.UserId })
            .IsUnique();

        // Whisper: from player + targets
        modelBuilder.Entity<Whisper>()
            .HasIndex(w => new { w.GameId, w.CreatedAt })
            .IsDescending(new[] { false, true });

        // AgentCall: game + status indexes
        modelBuilder.Entity<AgentCall>()
            .HasIndex(a => new { a.GameId, a.Status, a.CreatedAt })
            .IsDescending(new[] { false, false, true });

        // Whisper targets stored as JSON-friendly string — DEPRECATED: use TargetPlayerIds instead
        // modelBuilder.Entity<Whisper>()
        //     .Property(w => w.Targets)
        //     .HasColumnType("text");

        // AgentCall input/output as text (JSON)
        modelBuilder.Entity<AgentCall>()
            .Property(a => a.Input)
            .HasColumnType("text");
        modelBuilder.Entity<AgentCall>()
            .Property(a => a.Output)
            .HasColumnType("text");
        modelBuilder.Entity<AgentCall>()
            .Property(a => a.OutputMessage)
            .HasColumnType("text");

        // AgentCall parent/child relationship
        modelBuilder.Entity<AgentCall>()
            .HasOne(a => a.ParentCall)
            .WithMany(a => a.ChildCalls)
            .HasForeignKey(a => a.ParentCallId)
            .OnDelete(DeleteBehavior.Cascade);

        // Player -> Character one-to-one (a player gets one character per game)
        modelBuilder.Entity<Player>()
            .HasOne(p => p.Character)
            .WithOne(c => c.Player)
            .HasForeignKey<Character>(c => c.PlayerId);

        // Whisper -> Session (optional)
        modelBuilder.Entity<Whisper>()
            .HasOne(w => w.Session)
            .WithMany()
            .HasForeignKey(w => w.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Value converter for AgentCall Metadata
        modelBuilder.Entity<AgentCall>()
            .Property(a => a.Metadata)
            .HasConversion(
                v => v == null ? "null" : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => string.IsNullOrEmpty(v) || v == "null" ? null! : JsonSerializer.Deserialize<JsonElement>(v)!);

        // LLM Preset
        modelBuilder.Entity<LLMPreset>()
            .HasIndex(p => new { p.UserId, p.Name })
            .IsUnique();
        modelBuilder.Entity<LLMPreset>()
            .HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Combat entities
        modelBuilder.Entity<Combat>()
            .HasMany(c => c.Participants)
            .WithOne(p => p.Combat)
            .HasForeignKey(p => p.CombatId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Combat>()
            .HasMany(c => c.Events)
            .WithOne(e => e.Combat)
            .HasForeignKey(e => e.CombatId)
            .OnDelete(DeleteBehavior.Cascade);

        // CombatParticipant JSON properties
        modelBuilder.Entity<CombatParticipant>()
            .Property(p => p.Conditions)
            .HasConversion(
                v => v.ValueKind == JsonValueKind.Undefined ? "null" : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => string.IsNullOrEmpty(v) || v == "null" ? JsonDocument.Parse("[]").RootElement : JsonSerializer.Deserialize<JsonElement>(v)!)
            .HasColumnType("jsonb");
        modelBuilder.Entity<CombatParticipant>()
            .Property(p => p.SavingThrows)
            .HasConversion(
                v => v == null ? "null" : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => string.IsNullOrEmpty(v) || v == "null" ? null! : JsonSerializer.Deserialize<JsonElement>(v)!)
            .HasColumnType("jsonb");
        modelBuilder.Entity<CombatParticipant>()
            .Property(p => p.DeathSaveState)
            .HasConversion(
                v => v == null ? "null" : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => string.IsNullOrEmpty(v) || v == "null" ? null! : JsonSerializer.Deserialize<JsonElement>(v)!)
            .HasColumnType("jsonb");
        modelBuilder.Entity<CombatParticipant>()
            .Property(p => p.TemporaryHP)
            .HasConversion(
                v => v == null ? "null" : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => string.IsNullOrEmpty(v) || v == "null" ? null! : JsonSerializer.Deserialize<JsonElement>(v)!)
            .HasColumnType("jsonb");
        modelBuilder.Entity<CombatEvent>()
            .Property(e => e.Metadata)
            .HasConversion(
                v => v == null ? "null" : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => string.IsNullOrEmpty(v) || v == "null" ? null! : JsonSerializer.Deserialize<JsonElement>(v)!)
            .HasColumnType("jsonb");

        // LLM Interaction Log
        modelBuilder.Entity<LLMInteractionLog>()
            .Property(l => l.SystemPrompt)
            .HasColumnType("text");
        modelBuilder.Entity<LLMInteractionLog>()
            .Property(l => l.UserPrompt)
            .HasColumnType("text");
        modelBuilder.Entity<LLMInteractionLog>()
            .Property(l => l.Response)
            .HasColumnType("text");
        modelBuilder.Entity<LLMInteractionLog>()
            .Property(l => l.RequestJson)
            .HasColumnType("text");
        modelBuilder.Entity<LLMInteractionLog>()
            .Property(l => l.ResponseJson)
            .HasColumnType("text");
        modelBuilder.Entity<LLMInteractionLog>()
            .HasOne(l => l.Preset)
            .WithMany()
            .HasForeignKey(l => l.PresetId)
            .OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<LLMInteractionLog>()
            .HasOne(l => l.User)
            .WithMany()
            .HasForeignKey(l => l.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<LLMInteractionLog>()
            .HasIndex(l => new { l.UserId, l.OriginGameId, l.StartedAt })
            .IsDescending(new[] { false, false, true });

        // Audit Log
        modelBuilder.Entity<AuditLog>()
            .HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<AuditLog>()
            .HasIndex(a => new { a.UserId, a.CreatedAt })
            .IsDescending(new[] { false, true });

        // Performance indexes
        // PlotThreads: query by game + status (used in RAG, PlotWeaver)
        modelBuilder.Entity<PlotThread>()
            .HasIndex(p => new { p.GameId, p.Status });

        // PlotThreads: full-text search on title and description (requires pg_trgm extension)
        // Note: GIN index with pg_trgm is applied via migration, not fluent API
        modelBuilder.Entity<PlotThread>()
            .HasIndex(p => new { p.GameId, p.Title })
            .HasDatabaseName("IX_PlotThreads_GameId_Title");

        // Combats: active combat queries
        modelBuilder.Entity<Combat>()
            .HasIndex(c => new { c.GameId, c.Status });

        // LLMInteractionLogs: date-based queries
        modelBuilder.Entity<LLMInteractionLog>()
            .HasIndex(l => l.StartedAt);

        // Messages: created date for pagination
        modelBuilder.Entity<Message>()
            .HasIndex(m => new { m.SessionId, m.CreatedAt })
            .IsDescending(new[] { false, true });

        // Session Notes
        modelBuilder.Entity<SessionNote>()
            .HasOne(sn => sn.Session)
            .WithMany()
            .HasForeignKey(sn => sn.SessionId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<SessionNote>()
            .HasOne(sn => sn.Creator)
            .WithMany()
            .HasForeignKey(sn => sn.CreatorId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<SessionNote>()
            .HasIndex(sn => new { sn.SessionId, sn.CreatedAt })
            .IsDescending(new[] { false, true });

        // Prompt Templates: game + type + name
        modelBuilder.Entity<PromptTemplate>()
            .HasOne(pt => pt.Game)
            .WithMany()
            .HasForeignKey(pt => pt.GameId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<PromptTemplate>()
            .HasOne(pt => pt.User)
            .WithMany()
            .HasForeignKey(pt => pt.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<PromptTemplate>()
            .HasIndex(pt => new { pt.GameId, pt.Type, pt.Name })
            .IsUnique();

        // Game Templates
        modelBuilder.Entity<GameTemplate>()
            .HasOne(gt => gt.User)
            .WithMany()
            .HasForeignKey(gt => gt.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<GameTemplate>()
            .HasOne(gt => gt.LLMPreset)
            .WithMany()
            .HasForeignKey(gt => gt.LLMPresetId)
            .OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<GameTemplate>()
            .HasIndex(gt => new { gt.UserId, gt.Name })
            .IsUnique();
        modelBuilder.Entity<GameTemplate>()
            .HasIndex(gt => new { gt.UserId, gt.CreatedAt })
            .IsDescending(new[] { false, true });

        // EventRecord
        modelBuilder.Entity<EventRecord>()
            .Property(e => e.EventType)
            .IsRequired();
        modelBuilder.Entity<EventRecord>()
            .Property(e => e.Payload)
            .IsRequired();
        modelBuilder.Entity<EventRecord>()
            .HasIndex(e => new { e.GameId, e.Status });
        modelBuilder.Entity<EventRecord>()
            .HasIndex(e => new { e.Status, e.CreatedAt });
        modelBuilder.Entity<EventRecord>()
            .HasIndex(e => e.CorrelationId)
            .IsUnique();

        // ==================== Soft-Delete Query Filters ====================
        // Automatically exclude soft-deleted entities from all queries
        modelBuilder.Entity<Game>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Player>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Message>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Character>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<NPC>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<PlotThread>().HasQueryFilter(e => !e.IsDeleted);
    }
}
