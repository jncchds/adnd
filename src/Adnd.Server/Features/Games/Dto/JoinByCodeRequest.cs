using FluentValidation;

namespace Adnd.Server.Features.Games.Dto;

public class JoinByCodeRequest
{
    public string JoinCode { get; set; } = string.Empty;
}

public class JoinByCodeRequestValidator : AbstractValidator<JoinByCodeRequest>
{
    public JoinByCodeRequestValidator()
    {
        RuleFor(x => x.JoinCode).NotEmpty().Length(8);
    }
}
