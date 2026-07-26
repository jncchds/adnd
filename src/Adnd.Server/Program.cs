using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Adnd.Server.Agent;
using Adnd.Server.Data;
using Adnd.Server.Services;
using Adnd.Server.Services.HealthChecks;
using Adnd.Server.Services.Llm;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Wolverine;
using Wolverine.Postgresql;

var builder = WebApplication.CreateBuilder(args);

// ── MVC / API ──────────────────────────────────────────────────────────────
builder.Services.AddControllers(opts =>
    {
        opts.Conventions.Add(new RouteTokenTransformerConvention(new LowerCaseParameterTransformer()));
    })
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "ADnD API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            []
        }
    });
});

// ── Database ────────────────────────────────────────────────────────────────
var connStr = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured.");

builder.Services.AddDbContext<AppDbContext>(opts =>
    opts.UseNpgsql(connStr, o => o.UseVector()));

builder.Services.AddScoped<MigrationService>();

// ── Auth ─────────────────────────────────────────────────────────────────────
var jwtSecret = builder.Configuration["JwtSettings:SecretKey"]
    ?? throw new InvalidOperationException("JwtSettings:SecretKey not configured.");
var jwtIssuer = builder.Configuration["JwtSettings:Issuer"]
    ?? throw new InvalidOperationException("JwtSettings:Issuer not configured.");
var jwtAudience = builder.Configuration["JwtSettings:Audience"]
    ?? throw new InvalidOperationException("JwtSettings:Audience not configured.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
        // Extract JWT from query-string for SignalR WebSocket connections
        opts.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var token = ctx.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) &&
                    ctx.Request.Path.StartsWithSegments("/gamehub"))
                {
                    ctx.Token = token;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// ── Services ─────────────────────────────────────────────────────────────────
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserIdProvider, UserIdProvider>();
builder.Services.AddSingleton<IApiKeyEncryptionService, ApiKeyEncryptionService>();
builder.Services.AddScoped<ILLMPresetService, LLMPresetService>();
builder.Services.AddScoped<IGameAuthorizationService, GameAuthorizationService>();
builder.Services.AddScoped<IGameManagementService, GameManagementService>();
builder.Services.AddScoped<ISessionManagementService, SessionManagementService>();
builder.Services.AddSingleton<IDiceEngine, DiceEngine>();
builder.Services.AddSingleton<ISystemRegistry, SystemRegistry>();
builder.Services.AddScoped<IGameEngine, GameEngine>();
builder.Services.AddScoped<IPlayerManagementService, PlayerManagementService>();
builder.Services.AddScoped<IWhisperService, WhisperService>();

// ── LLM Provider System ───────────────────────────────────────────────────────
builder.Services.AddHttpClient("LLMProvider").ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(120));
builder.Services.AddSingleton<ILLMProviderFactory, LLMProviderFactory>();
builder.Services.AddScoped<IEmbeddingService, EmbeddingService>();
builder.Services.AddScoped<ILLMInteractionLogger, LLMInteractionLogger>();
builder.Services.AddSingleton<IResiliencePolicies, ResiliencePolicies>();

// ── Event / Agent System ──────────────────────────────────────────────────────
builder.Services.AddScoped<IEventBus, EventBusWorker>();
builder.Services.AddScoped<IAgentBus, AgentBus>();
builder.Services.AddSingleton<IDeadLetterQueue, DeadLetterQueue>();
builder.Services.AddScoped<IGMToolRegistry, GMToolRegistry>();
builder.Services.AddScoped<IGMToolCallService, GMToolCallService>();
builder.Services.AddSingleton<IHandlerRegistry, HandlerRegistry>();
builder.Services.AddSingleton<IGameAgentManager, GameAgentManager>();
builder.Services.AddHostedService(sp => (GameAgentManager)sp.GetRequiredService<IGameAgentManager>());

// ── Plot Intelligence (Phase 7) ───────────────────────────────────────────────
builder.Services.AddScoped<IRAGService, RAGService>();
builder.Services.AddScoped<IPlotWeaver, PlotWeaver>();
builder.Services.AddScoped<ICharacterCreationFactory, CharacterCreationFactory>();
builder.Services.AddScoped<INarrativeGenerationFactory, NarrativeGenerationFactory>();
builder.Services.AddScoped<IGameStartService, GameStartService>();

// ── Hangfire (background jobs, PostgreSQL-backed) ─────────────────────────────
builder.Services.AddHangfire(config =>
    config.SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
          .UseSimpleAssemblyNameTypeSerializer()
          .UseRecommendedSerializerSettings()
          .UsePostgreSqlStorage(o => o.UseNpgsqlConnection(connStr)));
builder.Services.AddHangfireServer(opts => opts.WorkerCount = 2);

// ── Combat System ─────────────────────────────────────────────────────────────
builder.Services.AddScoped<Adnd.Server.Services.Combat.CombatEventLogger>();
builder.Services.AddScoped<Adnd.Server.Services.Combat.ICombatLifecycleService, Adnd.Server.Services.Combat.CombatLifecycleService>();
builder.Services.AddScoped<Adnd.Server.Services.Combat.ICombatParticipantService, Adnd.Server.Services.Combat.CombatParticipantService>();
builder.Services.AddScoped<Adnd.Server.Services.Combat.ICombatInitiativeService, Adnd.Server.Services.Combat.CombatInitiativeService>();
builder.Services.AddScoped<Adnd.Server.Services.Combat.ICombatTurnService, Adnd.Server.Services.Combat.CombatTurnService>();
builder.Services.AddScoped<Adnd.Server.Services.Combat.ICombatStateService, Adnd.Server.Services.Combat.CombatStateService>();
builder.Services.AddScoped<Adnd.Server.Services.Combat.ICombatSpellService, Adnd.Server.Services.Combat.CombatSpellService>();
builder.Services.AddScoped<Adnd.Server.Services.Combat.ICombatInventoryService, Adnd.Server.Services.Combat.CombatInventoryService>();
builder.Services.AddScoped<Adnd.Server.Services.Combat.ICombatProgressionService, Adnd.Server.Services.Combat.CombatProgressionService>();
builder.Services.AddScoped<Adnd.Server.Services.Combat.ICombatGridService, Adnd.Server.Services.Combat.CombatGridService>();
builder.Services.AddScoped<Adnd.Server.Services.Combat.ICombatAIService, Adnd.Server.Services.Combat.CombatAIService>();
builder.Services.AddScoped<Adnd.Server.Services.Combat.ICombatQueryService, Adnd.Server.Services.Combat.CombatQueryService>();
builder.Services.AddScoped<Adnd.Server.Services.Combat.ISANService, Adnd.Server.Services.Combat.SANService>();
builder.Services.AddScoped<Adnd.Server.Services.Combat.ICombatService, Adnd.Server.Services.Combat.CombatService>();

// ── Wolverine ─────────────────────────────────────────────────────────────────
builder.Host.UseWolverine(opts =>
{
    opts.PersistMessagesWithPostgresql(connStr, "public");
    opts.Discovery.IncludeAssembly(typeof(Program).Assembly);
});

// ── SignalR ───────────────────────────────────────────────────────────────────
builder.Services.AddSignalR();

// ── Health Checks ─────────────────────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database")
    .AddCheck<LlmProvidersHealthCheck>("llm", tags: ["ready"])
    .AddCheck<PgVectorHealthCheck>("pgvector", tags: ["ready"]);

// ── CORS ──────────────────────────────────────────────────────────────────────
builder.Services.AddCors(opts =>
    opts.AddPolicy("AllowAll", p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

// ── Rate Limiting ─────────────────────────────────────────────────────────────
var globalPermit = int.TryParse(builder.Configuration["RateLimiting:GlobalPermitLimit"], out var gp) ? gp : 300;
var authPermit   = int.TryParse(builder.Configuration["RateLimiting:AuthPermitLimit"],   out var ap) ? ap : 20;
var llmPermit    = int.TryParse(builder.Configuration["RateLimiting:LLMPresetPermitLimit"], out var lp) ? lp : 10;

builder.Services.AddRateLimiter(opts =>
{
    opts.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            ctx.Connection.RemoteIpAddress?.ToString() ?? "anon",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = globalPermit, Window = TimeSpan.FromMinutes(1) }));

    opts.AddFixedWindowLimiter("auth", o => { o.PermitLimit = authPermit; o.Window = TimeSpan.FromMinutes(1); });
    opts.AddFixedWindowLimiter("llm",  o => { o.PermitLimit = llmPermit;  o.Window = TimeSpan.FromMinutes(1); });
    opts.OnRejected = async (ctx, _) =>
    {
        ctx.HttpContext.Response.StatusCode = 429;
        await ctx.HttpContext.Response.WriteAsync("Rate limit exceeded.");
    };
});

var app = builder.Build();

// ── Migrations ────────────────────────────────────────────────────────────────
await app.UseDatabaseMigrationsAsync();

// ── Swagger ────────────────────────────────────────────────────────────────────
app.UseSwagger();
app.UseSwaggerUI();

// ── Hangfire ──────────────────────────────────────────────────────────────────
app.UseHangfireDashboard("/hangfire");

// ── Health endpoints ──────────────────────────────────────────────────────────
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready");

// ── Static files + SPA ────────────────────────────────────────────────────────
app.UseDefaultFiles();
app.UseStaticFiles();

// ── Middleware pipeline ───────────────────────────────────────────────────────
app.UseCors("AllowAll");
app.UseRouting();

// Security headers
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["X-Frame-Options"] = "DENY";
    ctx.Response.Headers["X-XSS-Protection"] = "1; mode=block";
    ctx.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    ctx.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    await next();
});

app.UseForwardedHeaders();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// ── Endpoints ─────────────────────────────────────────────────────────────────
app.MapControllers();
app.MapHub<Adnd.Server.Hubs.GameHub>("/gamehub");
app.MapFallbackToFile("index.html");

app.Run();

// Transforms route tokens to lowercase: GamesController → /api/games
public class LowerCaseParameterTransformer : IOutboundParameterTransformer
{
    public string? TransformOutbound(object? value) =>
        value?.ToString()?.ToLowerInvariant();
}
