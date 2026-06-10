using FluentValidation;

namespace Adnd.Server.Features.Games.Dto;

public class UpdateGameSystemRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? RulesetConfig { get; set; }
}

public class UpdateGameSystemRequestValidator : AbstractValidator<UpdateGameSystemRequest>
{
    public UpdateGameSystemRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Description).MaximumLength(512);
    }
}
