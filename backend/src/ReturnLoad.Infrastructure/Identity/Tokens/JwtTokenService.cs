using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using ReturnLoad.Application.Identity;
using ReturnLoad.Shared.Configuration;

namespace ReturnLoad.Infrastructure.Identity.Tokens;

/// <summary>
/// Assembles the JWT access token with the ADR-0013 claim set and signs it via
/// <see cref="ITokenSigner"/> (algorithm-agnostic). Lifetime comes from <see cref="JwtOptions"/>.
/// </summary>
internal sealed class JwtTokenService : ITokenService
{
    private readonly ITokenSigner _signer;
    private readonly JwtOptions _options;

    public JwtTokenService(ITokenSigner signer, IOptions<JwtOptions> options)
    {
        _signer = signer;
        _options = options.Value;
    }

    public AccessToken CreateAccessToken(AccessTokenDescriptor descriptor)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DateTimeOffset expires = now.AddMinutes(_options.AccessTokenMinutes);

        List<Claim> claims =
        [
            new(JwtRegisteredClaimNames.Sub, descriptor.UserId.ToString()),
            new(AppClaims.UserId, descriptor.UserId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(AppClaims.PermissionsVersion, descriptor.PermissionsVersion.ToString()),
        ];

        if (!string.IsNullOrEmpty(descriptor.Email))
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Email, descriptor.Email));
        }

        if (!string.IsNullOrEmpty(descriptor.DeviceId))
        {
            claims.Add(new Claim(AppClaims.DeviceId, descriptor.DeviceId));
        }

        foreach (string role in descriptor.Roles)
        {
            claims.Add(new Claim("role", role));
        }

        JwtSecurityToken token = new(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: _signer.CreateSigningCredentials());

        string encoded = new JwtSecurityTokenHandler().WriteToken(token);
        return new AccessToken(encoded, expires);
    }
}
