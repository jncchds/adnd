using Adnd.Server.Data;
using Adnd.Server.Services;
using Adnd.Server.Services.Llm;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Controllers;

[ApiController]
[Route("api/llm")]
[Authorize]
public class LLMController(
    ILLMProviderFactory providerFactory,
    IApiKeyEncryptionService encryption,
    IUserIdProvider userIdProvider,
    AppDbContext db,
    ILogger<LLMController> logger) : ControllerBase
{
    /// <summary>Returns status for all LLM presets belonging to the current user.</summary>
    [HttpGet("providers")]
    public async Task<IActionResult> GetProviders(CancellationToken ct)
    {
        var userId = userIdProvider.GetUserId();

        // Scoped to the caller. This query had no UserId filter, so it returned every
        // user's preset configuration and probed their private inference endpoints.
        var presets = await db.LLMPresets
            .Where(p => p.UserId == userId && p.IsActive)
            .ToListAsync(ct);

        var statuses = new List<object>();
        foreach (var preset in presets)
        {
            if (preset.ApiKey is not null)
                preset.DecryptedApiKey = encryption.Decrypt(preset.ApiKey);

            ProviderStatus status;
            try
            {
                var provider = providerFactory.CreateFromPreset(preset);
                status = await provider.GetStatusAsync(ct);
            }
            catch (Exception ex)
            {
                // Log the detail; don't echo upstream error bodies (which can carry endpoint
                // URLs and provider diagnostics) back over the API.
                logger.LogWarning(ex, "Status probe failed for LLM preset {PresetId}", preset.Id);
                status = new ProviderStatus(false, null, "Provider unreachable.");
            }

            statuses.Add(new
            {
                preset.Id,
                preset.Name,
                preset.ProviderType,
                preset.BaseModel,
                Status = status
            });
        }

        return Ok(statuses);
    }
}
