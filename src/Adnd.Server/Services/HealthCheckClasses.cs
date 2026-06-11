using Microsoft.Extensions.Diagnostics.HealthChecks;
using Adnd.Server.Data;
using Adnd.Server.Services;
using Adnd.Server.Models;
using Microsoft.EntityFrameworkCore;

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
/// Checks all configured LLM presets for connectivity.
/// Returns Healthy if at least one preset is available, Unhealthy if all fail.
/// </summary>
public class LlmProvidersHealthCheck : IHealthCheck
{
    private readonly AppDbContext _context;
    private readonly ILLMProviderFactory _providerFactory;
    private readonly ILogger<LlmProvidersHealthCheck> _logger;

    public LlmProvidersHealthCheck(AppDbContext context, ILLMProviderFactory providerFactory, ILogger<LlmProvidersHealthCheck> logger)
    {
        _context = context;
        _providerFactory = providerFactory;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken token = default)
    {
        var presets = await _context.LLMPresets
            .Where(p => p.ApiKey != null)
            .ToListAsync(token);

        if (!presets.Any())
        {
            return HealthCheckResult.Healthy("No LLM presets configured — skipping health check.");
        }

        var healthy = 0;
        var unhealthy = 0;
        var errors = new List<string>();

        foreach (var preset in presets.Take(5)) // Check up to 5 presets
        {
            try
            {
                var provider = _providerFactory.CreateFromPreset(preset);
                var isAvailable = await provider.IsAvailableAsync();
                if (isAvailable)
                {
                    healthy++;
                }
                else
                {
                    unhealthy++;
                    errors.Add($"Preset '{preset.Name}' ({preset.ProviderType}): unavailable");
                }
            }
            catch (Exception ex)
            {
                unhealthy++;
                errors.Add($"Preset '{preset.Name}' ({preset.ProviderType}): {ex.Message}");
            }
        }

        if (healthy > 0)
        {
            return HealthCheckResult.Healthy($"{healthy}/{presets.Count} LLM presets available.");
        }

        return HealthCheckResult.Unhealthy($"All LLM presets unavailable. Errors: {string.Join("; ", errors)}");
    }
}

/// <summary>
/// pgvector embedding health check.
/// Verifies that the pgvector extension is installed and functional.
/// </summary>
public class PgVectorHealthCheck : IHealthCheck
{
    private readonly AppDbContext _context;

    public PgVectorHealthCheck(AppDbContext context)
    {
        _context = context;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken token = default)
    {
        try
        {
            // Verify pgvector extension is installed by querying for vector type support
            var result = await _context.Database
                .SqlQueryRaw<int>("SELECT 1 WHERE to_regclass('vector') IS NOT NULL")
                .FirstOrDefaultAsync(token);

            if (result == 1)
            {
                return HealthCheckResult.Healthy("pgvector extension is installed and functional.");
            }

            return HealthCheckResult.Unhealthy("pgvector extension is NOT installed. Run: CREATE EXTENSION IF NOT EXISTS vector;");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"pgvector health check failed: {ex.Message}");
        }
    }
}
