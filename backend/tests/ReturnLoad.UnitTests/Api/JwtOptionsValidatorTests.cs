using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using ReturnLoad.Api.Configuration;
using ReturnLoad.Shared.Configuration;

namespace ReturnLoad.UnitTests.Api;

public sealed class JwtOptionsValidatorTests
{
    private static JwtOptionsValidator ValidatorFor(string environment) =>
        new(new StubHostEnvironment(environment));

    [Fact]
    public void Development_accepts_missing_settings()
    {
        JwtOptionsValidator validator = ValidatorFor(Environments.Development);

        Assert.True(validator.Validate(null, new JwtOptions()).Succeeded);
    }

    [Fact]
    public void Production_fails_when_signing_key_is_missing()
    {
        JwtOptionsValidator validator = ValidatorFor(Environments.Production);

        JwtOptions options = new() { Issuer = "iss", Audience = "aud", SigningKey = "" };

        Assert.True(validator.Validate(null, options).Failed);
    }

    [Fact]
    public void Production_accepts_a_complete_configuration()
    {
        JwtOptionsValidator validator = ValidatorFor(Environments.Production);

        JwtOptions options = new()
        {
            Issuer = "https://returnload",
            Audience = "clients",
            SigningKey = "a-very-long-and-random-signing-key-value",
        };

        Assert.True(validator.Validate(null, options).Succeeded);
    }

    private sealed class StubHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
