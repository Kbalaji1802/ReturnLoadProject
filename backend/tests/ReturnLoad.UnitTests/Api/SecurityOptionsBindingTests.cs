using Microsoft.Extensions.Configuration;
using ReturnLoad.Api.Configuration;
using ReturnLoad.Shared.Configuration;

namespace ReturnLoad.UnitTests.Api;

public sealed class SecurityOptionsBindingTests
{
    private static IConfiguration Config(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Fact]
    public void RateLimitOptions_bind_from_the_expected_section()
    {
        IConfiguration config = Config(new Dictionary<string, string?>
        {
            ["RateLimiting:Global:PermitLimit"] = "42",
            ["RateLimiting:Global:WindowSeconds"] = "30",
            ["RateLimiting:Sensitive:PermitLimit"] = "5",
        });

        RateLimitOptions options = config.GetSection(RateLimitOptions.SectionName).Get<RateLimitOptions>()!;

        Assert.Equal(42, options.Global.PermitLimit);
        Assert.Equal(30, options.Global.WindowSeconds);
        Assert.Equal(5, options.Sensitive.PermitLimit);
    }

    [Fact]
    public void CorsOptions_bind_the_origin_allowlist()
    {
        IConfiguration config = Config(new Dictionary<string, string?>
        {
            ["Cors:AllowedOrigins:0"] = "https://admin.returnload.test",
            ["Cors:AllowCredentials"] = "true",
        });

        CorsOptions options = config.GetSection(CorsOptions.SectionName).Get<CorsOptions>()!;

        Assert.Equal(["https://admin.returnload.test"], options.AllowedOrigins);
        Assert.True(options.AllowCredentials);
    }

    [Fact]
    public void PasswordPolicy_defaults_are_hardened()
    {
        PasswordPolicyOptions policy = new();

        Assert.Equal(12, policy.MinLength);
        Assert.True(policy.RequireUppercase && policy.RequireLowercase
            && policy.RequireDigit && policy.RequireNonAlphanumeric);
    }
}
