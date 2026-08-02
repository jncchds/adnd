using System.Security.Claims;
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
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
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
        o.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        o.JsonSerializerOptions.Converters.Add(new Adnd.Server.Services.SafeJsonElementConverter());
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

// The appsettings placeholder is long enough to satisfy HS256, so without these checks a
// missing JwtSettings__SecretKey let the app boot and sign tokens with a value published
// in the repo — anyone could then forge a token for any user. Fail loudly instead.
if (jwtSecret is "OVERRIDE_IN_ENVIRONMENT" or "change_me")
    throw new InvalidOperationException(
        "JwtSettings:SecretKey is still the placeholder value. Set JwtSettings__SecretKey in the environment.");
if (Encoding.UTF8.GetByteCount(jwtSecret) < 32)
    throw new InvalidOperationException(
        "JwtSettings:SecretKey must be at least 32 bytes. Generate one with: openssl rand -base64 48");

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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            // Default is 5 minutes, which silently extends a 60-minute token to 65.
            ClockSkew = TimeSpan.FromSeconds(30)
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
builder.Services.AddSingleton<IOutboundUrlGuard, OutboundUrlGuard>();
builder.Services.AddScoped<ILLMPresetService, LLMPresetService>();
builder.Services.AddScoped<IGameAuthorizationService, GameAuthorizationService>();
builder.Services.AddScoped<IGameManagementService, GameManagementService>();
builder.Services.AddScoped<ISessionManagementService, SessionManagementService>();
builder.Services.AddSingleton<IDiceEngine, DiceEngine>();
builder.Services.AddSingleton<ISystemRegistry, SystemRegistry>();
builder.Services.AddScoped<IGameEngine, GameEngine>();
builder.Services.AddScoped<IPlayerManagementService, PlayerManagementService>();
builder.Services.AddScoped<IWhisperService, WhisperService>();
builder.Services.AddSingleton<IGmActivityBroadcaster, GmActivityBroadcaster>();

// ── LLM Provider System ───────────────────────────────────────────────────────
builder.Services.AddHttpClient("LLMProvider").ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(120));
builder.Services.AddSingleton<ILLMProviderFactory, LLMProviderFactory>();
builder.Services.AddScoped<IEmbeddingService, EmbeddingService>();
builder.Services.AddSingleton<ILLMInteractionLogger, LLMInteractionLogger>();
builder.Services.AddSingleton<IResiliencePolicies, ResiliencePolicies>();

// ── Event / Agent System ──────────────────────────────────────────────────────
builder.Services.AddScoped<IEventBus, EventBusWorker>();
builder.Services.AddScoped<IAgentBus, AgentBus>();
builder.Services.AddSingleton<IDeadLetterQueue, DeadLetterQueue>();
builder.Services.AddScoped<IGMToolRegistry, GMToolRegistry>();
builder.Services.AddScoped<IGMToolCallService, GMToolCallService>();
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
    opts.Durability.Mode = DurabilityMode.Solo;

    // EF Core registers DbContextOptions<AppDbContext> via an opaque lambda factory (AddDbContext),
    // which Wolverine's codegen can't inline. Since 6.0 that now throws InvalidServiceLocationException
    // at startup instead of silently falling back to GetRequiredService — route just this type through
    // the service locator instead of disabling the safety net for everything else.
    opts.CodeGeneration.AlwaysUseServiceLocationFor<DbContextOptions<AppDbContext>>();
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

// Partition key: the authenticated user when we have one, else the client IP. AddFixedWindowLimiter
// (as opposed to AddPolicy) creates a SINGLE bucket shared by the whole process — so 10 requests/min
// to /api/auth/login locked every user in the system out of logging in.
static string RateLimitPartitionKey(HttpContext ctx)
    => ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
       ?? ctx.Connection.RemoteIpAddress?.ToString()
       ?? "anon";

builder.Services.AddRateLimiter(opts =>
{
    opts.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            RateLimitPartitionKey(ctx),
            _ => new FixedWindowRateLimiterOptions { PermitLimit = globalPermit, Window = TimeSpan.FromMinutes(1) }));

    opts.AddPolicy("auth", ctx => RateLimitPartition.GetFixedWindowLimiter(
        RateLimitPartitionKey(ctx),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = authPermit, Window = TimeSpan.FromMinutes(1) }));

    opts.AddPolicy("llm", ctx => RateLimitPartition.GetFixedWindowLimiter(
        RateLimitPartitionKey(ctx),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = llmPermit, Window = TimeSpan.FromMinutes(1) }));

    opts.OnRejected = async (ctx, _) =>
    {
        ctx.HttpContext.Response.StatusCode = 429;
        await ctx.HttpContext.Response.WriteAsync("Rate limit exceeded.");
    };
});

// UseForwardedHeaders() with no configuration processes nothing (the default is None), so
// X-Forwarded-For was ignored and every request behind the proxy shared one partition.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // The reverse proxy is not at a fixed address in a container network; trust the
    // immediate hop only. Tighten with KnownProxies/KnownNetworks for a fixed deployment.
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
    o.ForwardLimit = 1;
});

var app = builder.Build();

// ── Migrations ────────────────────────────────────────────────────────────────
await app.UseDatabaseMigrationsAsync();

// ── Middleware pipeline ───────────────────────────────────────────────────────
// Order matters. Forwarded headers must come first so downstream middleware sees the real
// client IP, and the security headers must run before static files — previously
// UseStaticFiles short-circuited above them, so "/" and every /assets/*.js shipped with no
// X-Frame-Options and bypassed rate limiting entirely.
app.UseForwardedHeaders();

app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["X-Frame-Options"] = "DENY";
    ctx.Response.Headers["X-XSS-Protection"] = "1; mode=block";
    ctx.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    ctx.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    ctx.Response.Headers["Content-Security-Policy"] =
        "default-src 'self'; " +
        "script-src 'self'; " +
        // MUI injects styles at runtime, so inline styles are required.
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data: blob:; " +
        "font-src 'self' data:; " +
        "connect-src 'self' ws: wss:; " +
        "frame-ancestors 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'";
    await next();
});

// No HTTPS redirection or HSTS by design — TLS terminates at the reverse proxy.

app.UseGlobalExceptionHandler();

// ── Static files + SPA ────────────────────────────────────────────────────────
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseCors("AllowAll");
app.UseRouting();

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// ── Swagger ────────────────────────────────────────────────────────────────────
// Swagger UI is a plain browser page and this API authenticates with a bearer token held
// in localStorage, so it cannot be put behind [Authorize] without breaking it. It instead
// defaults to Development-only and must be opted into explicitly elsewhere — it was
// previously served anonymously in Production, publishing the whole attack surface.
var swaggerEnabled = bool.TryParse(builder.Configuration["Swagger:Enabled"], out var se)
    ? se
    : app.Environment.IsDevelopment();

if (swaggerEnabled)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ── Hangfire ──────────────────────────────────────────────────────────────────
// Explicit authorization. The library default is LocalRequestsOnly, which treats every
// request as local when a reverse proxy runs on the same host — handing out full job
// control (including job arguments) to anyone who can reach it.
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = [new HangfireDashboardAuthorizationFilter(builder.Configuration)]
});

// ── Health endpoints ──────────────────────────────────────────────────────────
// Liveness excludes the external dependency checks; otherwise an LLM provider outage
// makes an orchestrator restart-loop a perfectly healthy container.
app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = c => !c.Tags.Contains("ready") });
app.MapHealthChecks("/health/ready");

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
