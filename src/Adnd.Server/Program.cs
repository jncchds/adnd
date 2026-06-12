using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Migrations;

using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Text;
using Adnd.Server.Data;
using Adnd.Server.Hubs;
using Adnd.Server.Services;
using Adnd.Server.Agent;
using Adnd.Server.Handlers;
using MediatR;
using Npgsql.EntityFrameworkCore.PostgreSQL;
using Pgvector;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        opts.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddHttpContextAccessor();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ADnD API",
        Version = "v1",
        Description = "Advanced Dungeon Network — a multi-system TTRPG web framework with LLM-powered Game Master assistance, real-time chat, and custom system support.",
        Contact = new OpenApiContact
        {
            Name = "ADnD Support",
            Url = new Uri("https://github.com/adnd/api-docs")
        }
    });

    // Include XML documentation comments
    var xmlFile = $"{typeof(Program).Assembly.GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }

    // Add JWT auth to Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT token in the format: `Bearer <token>`"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

    // Custom operation filter for adding tags and descriptions
    c.OperationFilter<SwaggerOperationFilter>();

    // Group endpoints by controller name
    c.CustomSchemaIds(type => type.Name);
});

// SignalR
builder.Services.AddSignalR();

// CORS (for dev)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader());
});



// DB Context — PostgreSQL
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default"),
        npgsqlOptions => npgsqlOptions
            .MigrationsAssembly(typeof(AppDbContext).Assembly)
            .UseVector())
        .ConfigureWarnings(w => w
            .Ignore(RelationalEventId.PendingModelChangesWarning)
            .Ignore((EventId)10620)));

// Auth
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!))
    };
});

builder.Services.AddAuthorization();

// Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<MigrationService>();
builder.Services.AddScoped<IUserIdProvider, UserIdProvider>();
builder.Services.AddScoped<IGameAuthorizationService, GameAuthorizationService>();

// Game Engine
builder.Services.AddScoped<IDiceEngine, DiceEngine>();
builder.Services.AddScoped<ISystemRulesFactory, SystemRulesFactory>();
builder.Services.AddScoped<ISystemRegistry, SystemRegistry>();
builder.Services.AddScoped<IGameEngine, GameEngine>();

// Combat — domain services (extracted from monolithic CombatService)
builder.Services.AddScoped<ICombatActionFactory, CombatActionFactory>();
builder.Services.AddScoped<ICombatLifecycleService, CombatLifecycleService>();
builder.Services.AddScoped<ICombatParticipantService, CombatParticipantService>();
builder.Services.AddScoped<ICombatInitiativeService, CombatInitiativeService>();
builder.Services.AddScoped<ICombatTurnService, CombatTurnService>();
builder.Services.AddScoped<ICombatStateService, CombatStateService>();
builder.Services.AddScoped<ICombatSpellService, CombatSpellService>();
builder.Services.AddScoped<ICombatInventoryService, CombatInventoryService>();
builder.Services.AddScoped<ICombatProgressionService, CombatProgressionService>();
builder.Services.AddScoped<ICombatGridService, CombatGridService>();
builder.Services.AddScoped<ICombatAIService, CombatAIService>();
builder.Services.AddScoped<ICombatQueryService, CombatQueryService>();
builder.Services.AddScoped<ISANService, SANService>();
builder.Services.AddScoped<ICombatService, CombatService>();

// Agent Framework
builder.Services.AddScoped<IAgentBus, AgentBus>();
builder.Services.AddScoped<IDeadLetterQueue, DeadLetterQueue>();

// GM Tool Registry — defines and executes tools available to the GM agent
builder.Services.AddScoped<IGMToolRegistry, GMToolRegistry>();
builder.Services.AddScoped<IGMToolCallService, GMToolCallService>();

// Game Agent (per-game, singleton manager)
builder.Services.AddSingleton<IGameAgentManager, GameAgentManager>();

// MediatR — event-driven architecture
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(GameLifecycleHandler).Assembly);
});

// Whisper Service
builder.Services.AddScoped<IWhisperService, WhisperService>();

// API Key Encryption
builder.Services.AddScoped<IApiKeyEncryptionService, ApiKeyEncryptionService>();

// Resilience Policies (Polly)
builder.Services.AddSingleton<IResiliencePolicies, ResiliencePolicies>();

// Health Checks
builder.Services.AddHealthChecks()
    .AddCheck("liveness", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Application is running."))
    .AddCheck<Adnd.Server.HealthChecks.DatabaseHealthCheck>("database")
    .AddCheck<Adnd.Server.HealthChecks.LlmProvidersHealthCheck>("llm-providers")
    .AddCheck<Adnd.Server.HealthChecks.PgVectorHealthCheck>("pgvector");

// LLM Providers
builder.Services.AddHttpClient();
// Named HttpClient for LLM providers — enables connection pooling and timeout configuration
builder.Services.AddHttpClient("LLMProvider").ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(60));

// All LLM providers are created from per-game LLMPreset records via ILLMProviderFactory.
// There is no global provider registry — each game uses its own preset.
builder.Services.AddScoped<IRAGService, RAGService>();
builder.Services.AddScoped<IEmbeddingService, EmbeddingService>();
builder.Services.AddScoped<ILLMPresetService, LLMPresetService>();
builder.Services.AddScoped<ILLMInteractionLogger, LLMInteractionLogger>();

// PlotWeaver — automatic plot thread generation and evolution
builder.Services.AddScoped<IPlotWeaver, PlotWeaver>();

// Character creation factory (Strategy pattern)
builder.Services.AddScoped<ICharacterCreationFactory, CharacterCreationFactory>();

// Game start service + narrative generation (Strategy pattern)
builder.Services.AddScoped<IGameStartService, GameStartService>();
builder.Services.AddScoped<INarrativeGenerationFactory, NarrativeGenerationFactory>();

// LLM provider factory — creates ILLMProvider from LLMPreset at runtime
builder.Services.AddSingleton<ILLMProviderFactory, LLMProviderFactory>();

// Game/Session/Player management (FactoryMethod split from GamesController)
builder.Services.AddScoped<IGameManagementService, GameManagementService>();
builder.Services.AddScoped<ISessionManagementService, SessionManagementService>();
builder.Services.AddScoped<IPlayerManagementService, PlayerManagementService>();

// Quick-win features
builder.Services.AddScoped<ISessionNoteService, SessionNoteService>();
builder.Services.AddScoped<IPromptTemplateService, PromptTemplateService>();
builder.Services.AddScoped<IDiceStatsService, DiceStatsService>();
builder.Services.AddScoped<IGameTemplateService, GameTemplateService>();

// Maintenance service — periodic cleanup of stale data
builder.Services.AddHostedService<MaintenanceService>();

// Rate limiting configuration (bound from app settings)
builder.Services.AddRateLimitingOptions();

// Bind rate limiting config with comma-separated list support for docker-compose
var rateLimitingSection = builder.Configuration.GetSection("RateLimiting");
if (rateLimitingSection.Exists())
{
    builder.Services.Configure<RateLimitingOptions>(options =>
    {
        options.UseForwardedHeaders = rateLimitingSection.GetValue<bool>("UseForwardedHeaders", options.UseForwardedHeaders);
        options.TrustAllProxies = rateLimitingSection.GetValue<bool>("TrustAllProxies", options.TrustAllProxies);
        options.GlobalLimit = rateLimitingSection.GetValue<int>("GlobalLimit", options.GlobalLimit);
        options.GlobalWindowMinutes = rateLimitingSection.GetValue<int>("GlobalWindowMinutes", options.GlobalWindowMinutes);
        options.AuthLimit = rateLimitingSection.GetValue<int>("AuthLimit", options.AuthLimit);
        options.AuthWindowMinutes = rateLimitingSection.GetValue<int>("AuthWindowMinutes", options.AuthWindowMinutes);
        options.LlmPresetLimit = rateLimitingSection.GetValue<int>("LlmPresetLimit", options.LlmPresetLimit);
        options.LlmPresetWindowMinutes = rateLimitingSection.GetValue<int>("LlmPresetWindowMinutes", options.LlmPresetWindowMinutes);

        // Parse trusted proxies — supports both array indices and comma-separated
        var trustedProxiesRaw = rateLimitingSection["TrustedProxies"];
        if (!string.IsNullOrEmpty(trustedProxiesRaw))
        {
            options.TrustedProxies = trustedProxiesRaw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
        }
        else
        {
            // Try array indices format: TrustedProxies__0, TrustedProxies__1, etc.
            var proxyIndex = 0;
            while (true)
            {
                var proxy = rateLimitingSection[$"TrustedProxies__{proxyIndex}"];
                if (string.IsNullOrEmpty(proxy)) break;
                options.TrustedProxies.Add(proxy.Trim());
                proxyIndex++;
            }
        }
    });
}

// Forwarded headers for reverse proxy support (nginx, Caddy, etc.)
// This reads X-Forwarded-For / X-Real-IP headers to get the real client IP
// In Docker Compose, proxy IPs are dynamic — use TrustAllProxies to trust all
var rateLimitingConfig = builder.Configuration.GetSection("RateLimiting");
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.RequireHeaderSymmetry = false;

    // Trust all networks when TrustAllProxies is true (Docker Compose, k8s, etc.)
    // Otherwise, only trust the specific proxies in the config
    if (rateLimitingConfig.GetValue<bool>("TrustAllProxies", false))
    {
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
    }
    else if (rateLimitingConfig.Exists())
    {
        // Parse trusted proxies from config
        var trustedProxiesRaw = rateLimitingConfig["TrustedProxies"];
        if (!string.IsNullOrEmpty(trustedProxiesRaw))
        {
            foreach (var proxy in trustedProxiesRaw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                if (IPAddress.TryParse(proxy, out var ip))
                {
                    options.KnownProxies.Add(ip);
                }
                else if (System.Net.IPNetwork.TryParse(proxy, out var network))
                {
                    options.KnownIPNetworks.Add(network);
                }
            }
        }
    }
});

var app = builder.Build();

// Apply migrations on startup
app.UseDatabaseMigrations();

// Recover active game agents from database (survives restarts)
using (var scope = app.Services.CreateScope())
{
    var agentManager = scope.ServiceProvider.GetRequiredService<IGameAgentManager>();
    await agentManager.StartAllActiveGamesAsync();
}

// Configure the HTTP request pipeline
// Swagger is enabled in all environments for API documentation
app.UseSwagger();
app.UseSwaggerUI();

// Health Check endpoints: /health (liveness) and /health/ready (readiness)
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready");

if (app.Environment.IsDevelopment())
{
    app.UseCors("AllowAll");
}
else
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// Security Headers Middleware
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Append("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
    context.Response.Headers.Append("Cache-Control", "no-store, no-cache, must-revalidate");
    context.Response.Headers.Append("Pragma", "no-cache");
    await next();
});

// Forwarded Headers — MUST be before rate limiting so the real client IP is available
app.ConfigureForwardedHeaders();

// Rate Limiting Middleware — uses real client IP (respecting reverse proxy headers)
app.UseRateLimiting();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<GameHub>("/gamehub");

// Serve SPA fallback (production)
app.MapFallbackToFile("index.html");

app.Run();
