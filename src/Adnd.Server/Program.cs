using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Migrations;

using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text;
using Adnd.Server.Data;
using Adnd.Server.Hubs;
using Adnd.Server.Services;
using Adnd.Server.Agent;
using Adnd.Server.Handlers;
using MediatR;
using Npgsql.EntityFrameworkCore.PostgreSQL;

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
        npgsqlOptions => npgsqlOptions.MigrationsAssembly(typeof(AppDbContext).Assembly))
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

// GM Tool Registry — defines and executes tools available to the GM agent
builder.Services.AddScoped<IGMToolRegistry, GMToolRegistry>();

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
    .AddCheck<Adnd.Server.HealthChecks.LlmProvidersHealthCheck>("llm-providers");

// LLM Providers
builder.Services.AddHttpClient();

// Register the LLMProviderRegistry and auto-populate it with configured providers
builder.Services.AddSingleton<ILLMProviderRegistry>(sp =>
{
    var registry = new LLMProviderRegistry(sp.GetRequiredService<ILogger<LLMProviderRegistry>>());
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    var config = builder.Configuration;
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

    var ollamaConfig = config.GetSection("Ollama");
    if (!string.IsNullOrEmpty(ollamaConfig["BaseUrl"]))
    {
        var provider = new OllamaLLMProvider(
            loggerFactory.CreateLogger<OllamaLLMProvider>(),
            config, httpClientFactory);
        registry.RegisterProvider(provider.ProviderId, provider);
    }

    var lmStudioConfig = config.GetSection("LmStudio");
    if (!string.IsNullOrEmpty(lmStudioConfig["BaseUrl"]))
    {
        var provider = new LmStudioLLMProvider(
            loggerFactory.CreateLogger<LmStudioLLMProvider>(),
            config, httpClientFactory);
        registry.RegisterProvider(provider.ProviderId, provider);
    }

    var openAIConfig = config.GetSection("OpenAI");
    if (!string.IsNullOrEmpty(openAIConfig["ApiKey"]))
    {
        var provider = new OpenAILLMProvider(
            loggerFactory.CreateLogger<OpenAILLMProvider>(),
            config, httpClientFactory);
        registry.RegisterProvider(provider.ProviderId, provider);
    }

    var googleConfig = config.GetSection("Google");
    if (!string.IsNullOrEmpty(googleConfig["ApiKey"]))
    {
        var provider = new GoogleAIStudioLLMProvider(
            loggerFactory.CreateLogger<GoogleAIStudioLLMProvider>(),
            config, httpClientFactory);
        registry.RegisterProvider(provider.ProviderId, provider);
    }

    return registry;
});
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

// Player disconnect detector (background service)
builder.Services.AddHostedService<PlayerDisconnectDetector>(sp => new PlayerDisconnectDetector(
    sp,
    sp.GetRequiredService<ILogger<PlayerDisconnectDetector>>(),
    interval: TimeSpan.FromSeconds(30),
    timeout: TimeSpan.FromSeconds(60)
));

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

// Rate Limiting Middleware
app.UseRateLimiting();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<GameHub>("/gamehub");

// Serve SPA fallback (production)
app.MapFallbackToFile("index.html");

app.Run();
