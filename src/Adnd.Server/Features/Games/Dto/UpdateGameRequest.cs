using FluentValidation;

namespace Adnd.Server.Features.Games.Dto;

public class UpdateGameRequest
{
    public string Title { get; set; } = string.Empty;
    public string? PlotSeed { get; set; }
    public string? Status { get; set; }
}

public class UpdateGameRequestValidator : AbstractValidator<UpdateGameRequest>
{
    public UpdateGameRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Status).Must(s => s is null or "Draft" or "Active" or "Archived").When(x => x.Status != null);
    }
}
