using Microsoft.Extensions.Options;
using ReturnLoad.Shared.Configuration;

namespace ReturnLoad.Api.Configuration;

/// <summary>
/// Fails startup fast if JWT settings are missing outside Development, so a
/// misconfigured production host never boots able to "validate" tokens against an
/// empty key (03_TECHNICAL_BIBLE.md §7). Development is exempt so the foundation runs
/// without secrets while no endpoint requires them. The settings themselves live in
/// <see cref="JwtOptions"/> (Shared) so Infrastructure and API share one source of truth.
/// </summary>
public sealed class JwtOptionsValidator : IValidateOptions<JwtOptions>
{
    private readonly IHostEnvironment _environment;

    public JwtOptionsValidator(IHostEnvironment environment) => _environment = environment;

    public ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        if (_environment.IsDevelopment())
        {
            return ValidateOptionsResult.Success;
        }

        List<string> failures = [];
        if (string.IsNullOrWhiteSpace(options.Issuer))
        {
            failures.Add("Jwt:Issuer is required outside Development.");
        }

        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            failures.Add("Jwt:Audience is required outside Development.");
        }

        if (string.IsNullOrWhiteSpace(options.SigningKey))
        {
            failures.Add("Jwt:SigningKey must be supplied via a secret/environment variable outside Development.");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
