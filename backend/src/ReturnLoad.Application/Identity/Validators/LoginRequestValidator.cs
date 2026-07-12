using FluentValidation;

namespace ReturnLoad.Application.Identity.Validators;

/// <summary>Validates login input shape. Credential correctness is checked by the auth service.</summary>
public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(request => request.Email).NotEmpty().EmailAddress();
        RuleFor(request => request.Password).NotEmpty();
    }
}
