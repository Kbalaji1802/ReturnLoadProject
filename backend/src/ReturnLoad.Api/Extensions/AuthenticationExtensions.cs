using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ReturnLoad.Api.Configuration;
using ReturnLoad.Application.Identity;
using ReturnLoad.Shared.Configuration;

namespace ReturnLoad.Api.Extensions;

/// <summary>
/// Registers the JWT bearer authentication scheme and authorization services.
/// Framework wiring only — see <see cref="JwtOptions"/>.
/// </summary>
public static class AuthenticationExtensions
{
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind + fail-fast validate on start (JwtOptionsValidator: required outside Development).
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<JwtOptions>, JwtOptionsValidator>();

        JwtOptions jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? new JwtOptions();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Keep our claim names as issued ("sub", "role") rather than remapping.
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidAudience = jwtOptions.Audience,
                    IssuerSigningKey = string.IsNullOrWhiteSpace(jwtOptions.SigningKey)
                        ? null
                        : new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "sub",
                    RoleClaimType = "role",
                };
            });

        // Policy-based RBAC (ADR-0013): endpoints reference a policy name; the role
        // mapping lives in one place (AuthorizationPolicies.RoleMap). Endpoints are public
        // unless they opt in via [Authorize] or a policy.
        services.AddAuthorization(options =>
        {
            foreach ((string policy, string[] roles) in AuthorizationPolicies.RoleMap)
            {
                options.AddPolicy(policy, builder => builder.RequireRole(roles));
            }
        });

        return services;
    }
}
