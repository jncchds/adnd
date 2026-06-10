using FluentValidation;

namespace Adnd.Server.Features.Users.Dto;

public class UpdateDisplayNameRequest
{
    public string DisplayName { get; set; } = string.Empty;
}

public class UpdateDisplayNameRequestValidator : AbstractValidator<UpdateDisplayNameRequest>
{
    public UpdateDisplayNameRequestValidator()
    {
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(64);
    }
}
