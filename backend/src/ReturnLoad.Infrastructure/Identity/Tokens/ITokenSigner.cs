using Microsoft.IdentityModel.Tokens;

namespace ReturnLoad.Infrastructure.Identity.Tokens;

/// <summary>
/// Abstracts the JWT signing algorithm + key (ADR-0013). Token assembly
/// (<see cref="JwtTokenService"/>) depends on this, so moving from the MVP's symmetric
/// <b>HS256</b> to production's asymmetric <b>RS256 + JWKS</b> is a swap of the
/// implementation only — no change to token issuance or any business logic.
/// </summary>
internal interface ITokenSigner
{
    /// <summary>Signing credentials used when writing a token.</summary>
    SigningCredentials CreateSigningCredentials();
}
