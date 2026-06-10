using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Adnd.Server.Shared;
using Adnd.Server.Features.LlmPresets.Dto;
using System.Security.Claims;

namespace Adnd.Server.Features.LlmPresets;

[ApiController]
[Route("api/llm-presets")]
[Authorize]
public class LlmPresetController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly EncryptionService _encryption;

    public LlmPresetController(AppDbContext db, EncryptionService encryption)
    {
        _db = db;
        _encryption = encryption;
    }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Unauthorized();

        var presets = await _db.LlmPresets
            .Where(p => p.CreatedByUserId == userId)
            .OrderByDescending(p => p.UpdatedAt)
            .ToListAsync();

        var responses = presets.Select(MapToResponse);
        return Ok(responses);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateLlmPresetRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Unauthorized();

        var preset = new LlmPreset
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Provider = request.Provider,
            BaseModel = request.BaseModel,
            EmbeddingModel = request.EmbeddingModel,
            ApiKeyEncrypted = _encryption.Encrypt(request.ApiKey),
            SystemPrompt = request.SystemPrompt,
            Temperature = request.Temperature,
            MaxTokens = request.MaxTokens,
            IsActive = true,
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.LlmPresets.Add(preset);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = preset.Id }, MapToResponse(preset));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Unauthorized();

        var preset = await _db.LlmPresets
            .FirstOrDefaultAsync(p => p.Id == id && p.CreatedByUserId == userId);

        if (preset == null) return NotFound();

        return Ok(MapToResponse(preset));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateLlmPresetRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Unauthorized();

        var preset = await _db.LlmPresets
            .FirstOrDefaultAsync(p => p.Id == id && p.CreatedByUserId == userId);

        if (preset == null) return NotFound();

        preset.Name = request.Name;
        preset.Provider = request.Provider;
        preset.BaseModel = request.BaseModel;
        preset.EmbeddingModel = request.EmbeddingModel;
        preset.SystemPrompt = request.SystemPrompt;
        preset.Temperature = request.Temperature;
        preset.MaxTokens = request.MaxTokens;
        preset.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrEmpty(request.ApiKey))
            preset.ApiKeyEncrypted = _encryption.Encrypt(request.ApiKey);

        await _db.SaveChangesAsync();

        return Ok(MapToResponse(preset));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Unauthorized();

        var preset = await _db.LlmPresets
            .FirstOrDefaultAsync(p => p.Id == id && p.CreatedByUserId == userId);

        if (preset == null) return NotFound();

        preset.IsActive = false;
        preset.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok();
    }

    [HttpPost("{id}/activate")]
    public async Task<IActionResult> Activate(Guid id)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Unauthorized();

        var preset = await _db.LlmPresets
            .FirstOrDefaultAsync(p => p.Id == id && p.CreatedByUserId == userId);

        if (preset == null) return NotFound();

        preset.IsActive = true;
        preset.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok();
    }

    private LlmPresetResponse MapToResponse(LlmPreset preset) => new()
    {
        Id = preset.Id,
        Name = preset.Name,
        Provider = preset.Provider,
        BaseModel = preset.BaseModel,
        EmbeddingModel = preset.EmbeddingModel,
        SystemPrompt = preset.SystemPrompt,
        Temperature = preset.Temperature,
        MaxTokens = preset.MaxTokens,
        IsActive = preset.IsActive,
        CreatedAt = preset.CreatedAt,
        UpdatedAt = preset.UpdatedAt
    };
}
