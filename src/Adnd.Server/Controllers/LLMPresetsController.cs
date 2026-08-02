using Adnd.Server.Dtos;
using Adnd.Server.Models;
using Adnd.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Adnd.Server.Controllers;

[ApiController]
[Route("api/llmpresets")]
[Authorize]
[EnableRateLimiting("llm")]
public class LLMPresetsController(
    ILLMPresetService presets,
    IUserIdProvider userIdProvider) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetUserPresets()
    {
        var userId = userIdProvider.GetUserId();
        var result = await presets.GetUserPresetsAsync(userId);
        return Ok(result.Select(LLMPresetDto.From));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateLLMPresetDto dto)
    {
        try
        {
            var userId = userIdProvider.GetUserId();
            var preset = await presets.CreateAsync(userId, dto);
            return CreatedAtAction(nameof(GetById), new { id = preset.Id }, LLMPresetDto.From(preset));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        try
        {
            var userId = userIdProvider.GetUserId();
            var preset = await presets.GetByIdAsync(id, userId);
            return Ok(LLMPresetDto.From(preset));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateLLMPresetDto dto)
    {
        try
        {
            var userId = userIdProvider.GetUserId();
            var preset = await presets.UpdateAsync(id, userId, dto);
            return Ok(LLMPresetDto.From(preset));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            var userId = userIdProvider.GetUserId();
            await presets.DeleteAsync(id, userId);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/set-default")]
    public async Task<IActionResult> SetDefault(Guid id)
    {
        try
        {
            var userId = userIdProvider.GetUserId();
            await presets.SetDefaultAsync(id, userId);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpPost("models")]
    public async Task<IActionResult> QueryModels([FromBody] QueryModelsDto dto, CancellationToken ct)
    {
        try
        {
            var models = await presets.QueryModelsAsync(dto, ct);
            return Ok(models);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{id:guid}/models")]
    public async Task<IActionResult> ListModels(Guid id, CancellationToken ct)
    {
        try
        {
            var userId = userIdProvider.GetUserId();
            var models = await presets.ListModelsAsync(id, userId, ct);
            return Ok(models);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/test")]
    public async Task<IActionResult> TestConnection(Guid id, CancellationToken ct)
    {
        try
        {
            var userId = userIdProvider.GetUserId();
            var status = await presets.TestConnectionAsync(id, userId, ct);
            return Ok(status);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }
}
