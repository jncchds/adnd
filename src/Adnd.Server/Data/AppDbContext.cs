using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Adnd.Server.Models;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using System.Text.Json;

namespace Adnd.Server.Data;

// Value converter for PGVector float[]? <-> JSON string
public class VectorValueConverter : Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<float[]?, string?>
{
    public VectorValueConverter() : base(
        v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
        v => string.IsNullOrEmpty(v) ? null! : JsonSerializer.Deserialize<float[]>(v) ?? Array.Empty<float>()) { }
}

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public AppDbContext() { }

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
    public DbSet<GMToolCall> GMToolCalls => Set<GMToolCall>();
    public DbSet<Whisper> Whispers => Set<Whisper>();
    public DbSet<LLMPreset> LLMPresets => Set<LLMPreset>();
    public DbSet<LLMInteractionLog> LLMInteractionLogs => Set<LLMInteractionLog>();

    // Combat
    public DbSet<Combat> Combats => Set<Combat>();
    public DbSet<CombatParticipant> CombatParticipants => Set<CombatParticipant>();
    public DbSet<CombatEvent> CombatEvents => Set<CombatEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

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

        // Whisper targets stored as JSON-friendly string
        modelBuilder.Entity<Whisper>()
            .Property(w => w.Targets)
            .HasColumnType("text");

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
    }
}
