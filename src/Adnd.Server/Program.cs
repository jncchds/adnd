using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using Adnd.Server.Shared;
using Adnd.Server.Features.Auth;
using Adnd.Server.Features.LlmPresets;
using Adnd.Server.Data;
using FluentValidation;

var builder = WebApplication.CreateBuilder(args);

// Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("Default"),
        o => o.MigrationsHistoryTable("__EFMigrationsHistory")));

// JWT Auth
var jwtSecret = builder.Configuration["JwtSettings:SecretKey"] ?? "dev-secret-key-change-in-production";
var jwtIssuer = builder.Configuration["JwtSettings:Issuer"] ?? "adnd-server";
var jwtAudience = builder.Configuration["JwtSettings:Audience"] ?? "adnd-client";

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
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
    };
});

builder.Services.AddAuthorization();
builder.Services.AddControllers();

builder.Services.AddScoped<JwtService>();
builder.Services.AddScoped<EncryptionService>();
builder.Services.AddHttpClient<ModelLoaderService>();
builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly, ServiceLifetime.Transient);

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("Dev", policy =>
        policy.WithOrigins("http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

// Serve SPA static files (production)
app.MapWhen(ctx => !ctx.Request.Path.StartsWithSegments("/api") && !ctx.Request.Path.StartsWithSegments("/swagger"), appBuilder =>
{
    appBuilder.UseDefaultFiles();
    appBuilder.UseStaticFiles();
});

app.MapControllers();

// Swagger (dev only)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseCors("Dev");
}

app.Run();

// Seed predefined game systems
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
    foreach (var system in SeedData.DefaultSystems)
    {
        if (!await db.GameSystems.AnyAsync(s => s.Id == system.Id))
        {
            db.GameSystems.Add(system);
        }
    }
    await db.SaveChangesAsync();
}
