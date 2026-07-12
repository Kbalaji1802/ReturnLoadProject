using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ReturnLoad.Shared.Configuration;

namespace ReturnLoad.Infrastructure.Identity.Tokens;

/// <summary>
/// MVP HS256 (symmetric) signer keyed by <c>Jwt:SigningKey</c> (ADR-0013). The same key
/// signs and validates tokens. Production will replace this with an RS256/JWKS signer
/// without touching <see cref="JwtTokenService"/>.
/// </summary>
internal sealed class HmacTokenSigner : ITokenSigner
{
    private readonly SymmetricSecurityKey _key;

    public HmacTokenSigner(IOptions<JwtOptions> options)
    {
        string signingKey = options.Value.SigningKey;
        if (string.IsNullOrWhiteSpace(signingKey))
        {
            // Fail loudly rather than sign with an empty/insecure key.
            throw new InvalidOperationException(
                "Jwt:SigningKey is not configured; cannot issue tokens. Supply it via a secret/environment variable.");
        }

        _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
    }

    public SigningCredentials CreateSigningCredentials() => new(_key, SecurityAlgorithms.HmacSha256);
}
