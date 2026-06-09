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
/// </summary>
public class LlmProvidersHealthCheck : IHealthCheck
{
    private readonly ILLMProviderRegistry _registry;
    private readonly ILogger<LlmProvidersHealthCheck> _logger;

    public LlmProvidersHealthCheck(ILLMProviderRegistry registry, ILogger<LlmProvidersHealthCheck> logger)
    {
        _registry = registry;
        _logger = logger;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken token = default)
    {
        var providers = _registry.GetAllStatusAsync().ToList();
        var unhealthy = providers.Where(p => !p.IsAvailable).Select(p => p.ProviderId).ToList();

        if (unhealthy.Count > 0 && providers.Count == 0)
            return Task.FromResult(HealthCheckResult.Unhealthy($"All LLM providers unavailable: {string.Join(", ", unhealthy)}"));
        if (unhealthy.Count > 0)
            return Task.FromResult(HealthCheckResult.Degraded($"Some LLM providers unavailable: {string.Join(", ", unhealthy)}"));
        return Task.FromResult(HealthCheckResult.Healthy($"All {providers.Count} LLM providers healthy."));
    }
}
