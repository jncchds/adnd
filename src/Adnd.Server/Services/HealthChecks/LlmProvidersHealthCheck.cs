using Adnd.Server.Data;
using Adnd.Server.Services.Llm;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Adnd.Server.Services.HealthChecks;

/// <summary>
/// Probes the LLM providers actually configured as game defaults.
///
/// This used to just COUNT rows in the LLMPresets table and report Healthy, which said
/// nothing about whether any provider was reachable. It is tagged "ready", so it gates
/// readiness only — liveness must not depend on a third-party API being up.
/// </summary>
public class LlmProvidersHealthCheck(
    AppDbContext db,
    ILLMProviderFactory providerFactory,
    IApiKeyEncryptionService encryption) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        List<Models.LLMPreset> presets;
        try
        {
            presets = await db.LLMPresets
                .Where(p => p.IsActive && p.IsDefault)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Could not query LLM presets.", ex);
        }

        if (presets.Count == 0)
            return HealthCheckResult.Healthy("No default LLM preset configured.");

        var unreachable = new List<string>();

        foreach (var preset in presets)
        {
            try
            {
                if (preset.ApiKey is not null)
                    preset.DecryptedApiKey = encryption.Decrypt(preset.ApiKey);

                var provider = providerFactory.CreateFromPreset(preset);
                if (!await provider.IsAvailableAsync(cancellationToken))
                    unreachable.Add(preset.Name);
            }
            catch
            {
                unreachable.Add(preset.Name);
            }
        }

        if (unreachable.Count == 0)
            return HealthCheckResult.Healthy($"{presets.Count} default LLM preset(s) reachable.");

        // Degraded, not Unhealthy: the app still serves everything except GM narration.
        return HealthCheckResult.Degraded($"Unreachable LLM preset(s): {string.Join(", ", unreachable)}");
    }
}
