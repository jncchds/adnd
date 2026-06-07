using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using Adnd.Server.Data;
using Adnd.Server.Hubs;
using Adnd.Server.Services;
using Npgsql.EntityFrameworkCore.PostgreSQL;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
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
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

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
builder.Services.AddScoped<ISystemRegistry, SystemRegistry>();
builder.Services.AddScoped<IGameEngine, GameEngine>();

// Agent Framework
builder.Services.AddScoped<IAgentBus, AgentBus>();

// Whisper Service
builder.Services.AddScoped<IWhisperService, WhisperService>();

// LLM Providers
builder.Services.AddHttpClient();
builder.Services.AddScoped<ILLMProviderRegistry, LLMProviderRegistry>();
builder.Services.AddScoped<IRAGService, RAGService>();
builder.Services.AddScoped<ILLMPresetService, LLMPresetService>();
builder.Services.AddScoped<ILLMInteractionLogger, LLMInteractionLogger>();

// Register Ollama provider if configured
var ollamaConfig = builder.Configuration.GetSection("Ollama");
if (!string.IsNullOrEmpty(ollamaConfig["BaseUrl"]))
{
    builder.Services.AddScoped<ILLMProvider>(sp =>
        new OllamaLLMProvider(
            sp.GetRequiredService<ILogger<OllamaLLMProvider>>(),
            builder.Configuration,
            sp.GetRequiredService<IHttpClientFactory>()));
}

// Register LM Studio provider if configured
var lmStudioConfig = builder.Configuration.GetSection("LmStudio");
if (!string.IsNullOrEmpty(lmStudioConfig["BaseUrl"]))
{
    builder.Services.AddScoped<ILLMProvider>(sp =>
        new LmStudioLLMProvider(
            sp.GetRequiredService<ILogger<LmStudioLLMProvider>>(),
            builder.Configuration,
            sp.GetRequiredService<IHttpClientFactory>()));
}

// Register OpenAI provider if configured
var openAIConfig = builder.Configuration.GetSection("OpenAI");
if (!string.IsNullOrEmpty(openAIConfig["ApiKey"]))
{
    builder.Services.AddScoped<ILLMProvider>(sp =>
        new OpenAILLMProvider(
            sp.GetRequiredService<ILogger<OpenAILLMProvider>>(),
            builder.Configuration,
            sp.GetRequiredService<IHttpClientFactory>()));
}

// Register Google AI Studio provider if configured
var googleConfig = builder.Configuration.GetSection("Google");
if (!string.IsNullOrEmpty(googleConfig["ApiKey"]))
{
    builder.Services.AddScoped<ILLMProvider>(sp =>
        new GoogleAIStudioLLMProvider(
            sp.GetRequiredService<ILogger<GoogleAIStudioLLMProvider>>(),
            builder.Configuration,
            sp.GetRequiredService<IHttpClientFactory>()));
}

var app = builder.Build();

// Apply migrations on startup
app.UseDatabaseMigrations();

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
