using FluentValidation;

namespace Adnd.Server.Features.Games.Dto;

public class CreateGameSystemRequest
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? RulesetConfig { get; set; }
}

public class CreateGameSystemRequestValidator : AbstractValidator<CreateGameSystemRequest>
{
    public CreateGameSystemRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(32).Matches(@"^[a-z0-9]+$");
        RuleFor(x => x.Description).MaximumLength(512);
    }
}
