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
/// LLM providers health check — report-only.
/// Checks all configured LLM presets for connectivity and logs status.
/// Always returns Healthy since provider failures are handled by resilience policies (Polly).
/// A health check should not fail the app for transient downstream provider issues.
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
        var statuses = new List<string>();

        foreach (var preset in presets.Take(5)) // Check up to 5 presets
        {
            try
            {
                var provider = _providerFactory.CreateFromPreset(preset);
                var isAvailable = await provider.IsAvailableAsync();
                if (isAvailable)
                {
                    healthy++;
                    statuses.Add($"'{preset.Name}' ({preset.ProviderType}): available");
                }
                else
                {
                    unhealthy++;
                    statuses.Add($"'{preset.Name}' ({preset.ProviderType}): unavailable");
                    _logger.LogWarning("LLM preset '{PresetName}' ({ProviderType}) is unavailable", preset.Name, preset.ProviderType);
                }
            }
            catch (Exception ex)
            {
                unhealthy++;
                statuses.Add($"'{preset.Name}' ({preset.ProviderType}): {ex.Message}");
                _logger.LogWarning(ex, "LLM preset '{PresetName}' ({ProviderType}) health check failed", preset.Name, preset.ProviderType);
            }
        }

        // Always healthy — provider failures are handled by resilience policies (Polly retry + circuit breaker).
        // This check is diagnostic only; it should never fail the app for transient provider issues.
        return HealthCheckResult.Healthy($"{healthy}/{presets.Count} LLM presets available. {string.Join("; ", statuses)}");
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
                .SqlQueryRaw<int>("SELECT CASE WHEN to_regclass('vector') IS NOT NULL THEN 1 ELSE 0 END")
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
