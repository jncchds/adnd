using Microsoft.Extensions.Diagnostics.HealthChecks;
using Adnd.Server.Data;
using Adnd.Server.Services;

namespace Adnd.Server.HealthChecks;

/// <summary>
/// Database health check.
/// </summary>
public class DatabaseHealthCheck : IHealthCheck
{
    private readonly AppDbContext _context;

    public DatabaseHealthCheck(AppDbContext context)
    {
        _context = context;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken token = default)
    {
        try
        {
            await _context.Database.CanConnectAsync(token);
            return HealthCheckResult.Healthy("Database connection successful.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"Database connection failed: {ex.Message}");
        }
    }
}

/// <summary>
/// LLM providers health check.
/// Note: LLM providers are now created from per-game presets at runtime via ILLMProviderFactory.
/// There is no global provider registry to check. Health is verified per-game when a preset is used.
/// </summary>
public class LlmProvidersHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken token = default)
    {
        return Task.FromResult(HealthCheckResult.Healthy("LLM providers are configured per-game via LLM presets."));
    }
}
