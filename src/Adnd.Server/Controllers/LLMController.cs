using Adnd.Server.Data;
using Adnd.Server.Services.Llm;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Adnd.Server.Controllers;

[ApiController]
[Route("api/llm")]
[Authorize]
public class LLMController(ILLMProviderFactory providerFactory, AppDbContext db) : ControllerBase
{
    /// <summary>Returns status for all LLM presets belonging to the current user.</summary>
    [HttpGet("providers")]
    public async Task<IActionResult> GetProviders(CancellationToken ct)
    {
        var presets = await db.LLMPresets
            .Where(p => p.IsActive)
            .ToListAsync(ct);

        var statuses = new List<object>();
        foreach (var preset in presets)
        {
            try
            {
                var provider = providerFactory.CreateFromPreset(preset);
                var status = await provider.GetStatusAsync(ct);
                statuses.Add(new
                {
                    preset.Id,
                    preset.Name,
                    preset.ProviderType,
                    preset.BaseModel,
                    Status = status
                });
            }
            catch (Exception ex)
            {
                statuses.Add(new
                {
                    preset.Id,
                    preset.Name,
                    preset.ProviderType,
                    preset.BaseModel,
                    Status = new { IsAvailable = false, ModelName = (string?)null, Error = ex.Message }
                });
            }
        }

        return Ok(statuses);
    }
}
