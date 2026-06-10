using FluentValidation;

namespace Adnd.Server.Features.Games.Dto;

public class CreateGameRequest
{
    public string Title { get; set; } = string.Empty;
    public Guid SystemId { get; set; }
    public string? PlotSeed { get; set; }
}

public class CreateGameRequestValidator : AbstractValidator<CreateGameRequest>
{
    public CreateGameRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(128);
        RuleFor(x => x.SystemId).NotEmpty();
    }
}
