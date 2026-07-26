using Adnd.Server.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Adnd.Server.Services.HealthChecks;

public class LlmProvidersHealthCheck(AppDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var count = await db.LLMPresets.CountAsync(cancellationToken);
            return HealthCheckResult.Healthy($"LLM presets available: {count}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Degraded("Could not query LLM presets table.", ex);
        }
    }
}
