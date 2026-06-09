using Microsoft.AspNetCore.Mvc;
using Adnd.Server.Models;
using Adnd.Server.Services;

namespace Adnd.Server.Controllers;

public partial class AdminController
{
    // ==================== LLM Presets ====================

    [HttpGet("llm-presets")]
    public async Task<IActionResult> GetLLMPresets()
    {
        var userId = _userIdProvider.GetCurrentUserId();
        var presets = await _presetService.GetUserPresetsAsync(userId);
        return Ok(presets.Select(p => new
        {
            p.Id,
            p.Name,
            p.ProviderType,
            p.BaseModel,
            p.EndpointUrl,
            HasApiKey = !string.IsNullOrEmpty(p.ApiKey),
            p.Temperature,
            p.MaxTokens,
            p.TopP,
            p.EmbeddingModel,
            p.IsDefault,
            p.IsActive,
            p.CreatedAt,
            p.UpdatedAt
        }));
    }

    [HttpGet("llm-presets/{presetId}")]
    public async Task<IActionResult> GetLLMPreset(Guid presetId)
    {
        var userId = _userIdProvider.GetCurrentUserId();
        var preset = await _presetService.GetPresetAsync(userId, presetId);
        if (preset == null) return NotFound(new { error = "Preset not found." });

        return Ok(new
        {
            preset.Id,
            preset.Name,
            preset.ProviderType,
            preset.BaseModel,
            preset.EndpointUrl,
            HasApiKey = !string.IsNullOrEmpty(preset.ApiKey),
            preset.Temperature,
            preset.MaxTokens,
            preset.TopP,
            preset.FrequencyPenalty,
            preset.PresencePenalty,
            preset.Stream,
            preset.EmbeddingModel,
            preset.EmbeddingEndpointUrl,
            preset.IsDefault,
            preset.IsActive,
            preset.ExtraParams,
            preset.CreatedAt,
            preset.UpdatedAt
        });
    }

    [HttpPost("llm-presets")]
    public async Task<IActionResult> CreateLLMPreset([FromBody] CreateLLMPresetRequest request)
    {
        var userId = _userIdProvider.GetCurrentUserId();
        var preset = await _presetService.CreatePresetAsync(userId, request);
        return Ok(new { preset.Id, preset.Name, preset.ProviderType, preset.BaseModel });
    }

    [HttpPut("llm-presets/{presetId}")]
    public async Task<IActionResult> UpdateLLMPreset(Guid presetId, [FromBody] UpdateLLMPresetRequest request)
    {
        var userId = _userIdProvider.GetCurrentUserId();
        var preset = await _presetService.UpdatePresetAsync(userId, presetId, request);
        return Ok(new { preset.Id, preset.Name, preset.ProviderType, preset.BaseModel });
    }

    [HttpDelete("llm-presets/{presetId}")]
    public async Task<IActionResult> DeleteLLMPreset(Guid presetId)
    {
        var userId = _userIdProvider.GetCurrentUserId();
        await _presetService.DeletePresetAsync(userId, presetId);
        return Ok(new { message = "Preset deleted." });
    }

    [HttpPost("llm-presets/{presetId}/test")]
    public async Task<IActionResult> TestLLMPreset(Guid presetId)
    {
        var userId = _userIdProvider.GetCurrentUserId();
        try
        {
            var isConnected = await _presetService.TestConnectionAsync(userId, presetId);
            return Ok(new { success = isConnected, message = isConnected ? "Connection successful" : "Connection failed" });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("llm-presets/{presetId}/set-default")]
    public async Task<IActionResult> SetDefaultPreset(Guid presetId)
    {
        var userId = _userIdProvider.GetCurrentUserId();
        await _presetService.SetDefaultPresetAsync(userId, presetId);
        return Ok(new { message = "Default preset updated." });
    }

}
