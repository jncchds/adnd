using FluentValidation;

namespace Adnd.Server.Features.LlmPresets.Dto;

public class UpdateLlmPresetRequest
{
    public string Name { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string BaseModel { get; set; } = string.Empty;
    public string EmbeddingModel { get; set; } = string.Empty;
    public string? ApiKey { get; set; }
    public string SystemPrompt { get; set; } = string.Empty;
    public float Temperature { get; set; } = 0.7f;
    public float? MaxTokens { get; set; }
}

public class UpdateLlmPresetRequestValidator : AbstractValidator<UpdateLlmPresetRequest>
{
    public UpdateLlmPresetRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Provider).NotEmpty();
        RuleFor(x => x.BaseModel).NotEmpty();
        RuleFor(x => x.EmbeddingModel).NotEmpty();
        RuleFor(x => x.SystemPrompt).NotEmpty();
        RuleFor(x => x.Temperature).InclusiveBetween(0.0f, 2.0f);
    }
}
