using Microsoft.Extensions.Options;

namespace ReturnLoad.Api.Configuration;

/// <summary>
/// Strongly-typed JWT bearer settings, bound from the "Jwt" configuration section.
/// <para>
/// This wires the authentication <b>framework</b> only. There is deliberately no
/// token issuance, user store, or login endpoint yet — Identity &amp; Access is M2
/// (05_NEXT_TASKS.md). Real signing keys are supplied via environment variables,
/// never committed (01_PROJECT_RULES.md §1). The lifetime settings are carried now so
/// M2 issuance reads one source of truth.
/// </para>
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = string.Empty;

    public string Audience { get; init; } = string.Empty;

    public string SigningKey { get; init; } = string.Empty;

    /// <summary>Access-token lifetime in minutes (consumed by M2 issuance).</summary>
    public int AccessTokenMinutes { get; init; } = 15;

    /// <summary>Refresh-token lifetime in days (consumed by M2 issuance).</summary>
    public int RefreshTokenDays { get; init; } = 7;
}

/// <summary>
/// Fails startup fast if JWT settings are missing outside Development, so a
/// misconfigured production host never boots able to "validate" tokens against an
/// empty key (03_TECHNICAL_BIBLE.md §7). Development is exempt so the foundation runs
/// without secrets while no endpoint requires them.
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
