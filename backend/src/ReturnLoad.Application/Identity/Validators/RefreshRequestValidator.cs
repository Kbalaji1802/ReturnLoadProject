using FluentValidation;

namespace ReturnLoad.Application.Identity.Validators;

/// <summary>Validates that a refresh request carries a token.</summary>
public sealed class RefreshRequestValidator : AbstractValidator<RefreshRequest>
{
    public RefreshRequestValidator() => RuleFor(request => request.RefreshToken).NotEmpty();
}
