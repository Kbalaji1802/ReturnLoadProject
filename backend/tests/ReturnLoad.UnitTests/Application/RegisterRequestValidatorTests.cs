using Microsoft.Extensions.Options;
using ReturnLoad.Application.Identity;
using ReturnLoad.Application.Identity.Validators;
using ReturnLoad.Shared.Configuration;

namespace ReturnLoad.UnitTests.Application;

public sealed class RegisterRequestValidatorTests
{
    private static readonly RegisterRequestValidator Validator =
        new(Options.Create(new PasswordPolicyOptions()));

    [Fact]
    public void Accepts_a_strong_password_and_valid_email()
    {
        RegisterRequest request = new("driver@returnload.test", "Str0ng!Passw0rd", null, null);

        Assert.True(Validator.Validate(request).IsValid);
    }

    [Theory]
    [InlineData("short")]                 // too short
    [InlineData("alllowercase123!")]      // no uppercase
    [InlineData("ALLUPPERCASE123!")]      // no lowercase
    [InlineData("NoDigitsHere!!")]        // no digit
    [InlineData("NoSymbols12345")]        // no symbol
    public void Rejects_passwords_that_violate_the_policy(string password)
    {
        RegisterRequest request = new("driver@returnload.test", password, null, null);

        Assert.False(Validator.Validate(request).IsValid);
    }

    [Fact]
    public void Rejects_an_invalid_email()
    {
        RegisterRequest request = new("not-an-email", "Str0ng!Passw0rd", null, null);

        Assert.False(Validator.Validate(request).IsValid);
    }
}
