using FluentValidation;
using Microsoft.Extensions.Options;
using ReturnLoad.Shared.Configuration;

namespace ReturnLoad.Application.Identity.Validators;

/// <summary>
/// Validates registration input against the platform password policy (M1.5,
/// <see cref="PasswordPolicyOptions"/>). Produces clear per-field errors that surface in
/// the standard envelope (ADR-0008) before the identity store is touched.
/// </summary>
public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator(IOptions<PasswordPolicyOptions> policyOptions)
    {
        PasswordPolicyOptions policy = policyOptions.Value;

        RuleFor(request => request.Email)
            .NotEmpty().WithErrorCode("VALIDATION_ERROR")
            .EmailAddress().WithErrorCode("INVALID_EMAIL");

        RuleFor(request => request.Password)
            .NotEmpty().WithErrorCode("VALIDATION_ERROR")
            .MinimumLength(policy.MinLength).WithErrorCode("PASSWORD_TOO_SHORT")
            .MaximumLength(policy.MaxLength).WithErrorCode("PASSWORD_TOO_LONG");

        When(request => !string.IsNullOrEmpty(request.Password), () =>
        {
            if (policy.RequireUppercase)
            {
                RuleFor(r => r.Password).Matches("[A-Z]").WithErrorCode("PASSWORD_REQUIRES_UPPERCASE")
                    .WithMessage("Password must contain an uppercase letter.");
            }

            if (policy.RequireLowercase)
            {
                RuleFor(r => r.Password).Matches("[a-z]").WithErrorCode("PASSWORD_REQUIRES_LOWERCASE")
                    .WithMessage("Password must contain a lowercase letter.");
            }

            if (policy.RequireDigit)
            {
                RuleFor(r => r.Password).Matches("[0-9]").WithErrorCode("PASSWORD_REQUIRES_DIGIT")
                    .WithMessage("Password must contain a digit.");
            }

            if (policy.RequireNonAlphanumeric)
            {
                RuleFor(r => r.Password).Matches("[^a-zA-Z0-9]").WithErrorCode("PASSWORD_REQUIRES_SYMBOL")
                    .WithMessage("Password must contain a non-alphanumeric character.");
            }
        });
    }
}
