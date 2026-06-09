using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
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
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "ADnD API", Version = "v1" });

    // Add JWT auth to Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
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

// DB Context
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

// LLM Providers
builder.Services.AddHttpClient();

// Register the LLMProviderRegistry and auto-populate it with configured providers
builder.Services.AddScoped<ILLMProviderRegistry>(sp =>
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
builder.Services.AddScoped<ILLMPresetService, LLMPresetService>();
builder.Services.AddScoped<ILLMInteractionLogger, LLMInteractionLogger>();

// PlotWeaver — automatic plot thread generation and evolution
builder.Services.AddScoped<IPlotWeaver, PlotWeaver>();

// Character creation factory (Strategy pattern)
builder.Services.AddScoped<ICharacterCreationFactory, CharacterCreationFactory>();

// Game start service + narrative generation (Strategy pattern)
builder.Services.AddScoped<IGameStartService, GameStartService>();
builder.Services.AddScoped<INarrativeGenerationFactory, NarrativeGenerationFactory>();

// Game/Session/Player management (FactoryMethod split from GamesController)
builder.Services.AddScoped<IGameManagementService, GameManagementService>();
builder.Services.AddScoped<ISessionManagementService, SessionManagementService>();
builder.Services.AddScoped<IPlayerManagementService, PlayerManagementService>();

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
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
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
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<GameHub>("/gamehub");

// Serve SPA fallback (production)
app.MapFallbackToFile("index.html");

app.Run();
