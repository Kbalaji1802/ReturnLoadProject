using NetArchTest.Rules;
using ReturnLoad.Application.Abstractions.Identity;

namespace ReturnLoad.ArchitectureTests;

/// <summary>
/// Guards the authentication seam (ADR-0013): the auth contract lives in Application so
/// the API depends inward on an abstraction; the ASP.NET Core Identity mechanics live in
/// Infrastructure. This keeps the Clean Architecture dependency rule intact as auth grows.
/// </summary>
public sealed class IdentityArchitectureTests
{
    private static System.Reflection.Assembly InfrastructureAssembly =>
        typeof(ReturnLoad.Infrastructure.DependencyInjection).Assembly;

    [Fact]
    public void Auth_contract_lives_in_the_Application_layer()
    {
        Assert.StartsWith("ReturnLoad.Application", typeof(IAuthService).Namespace);
    }

    [Fact]
    public void Auth_implementation_lives_in_the_Infrastructure_layer()
    {
        TestResult result = Types.InAssembly(InfrastructureAssembly)
            .That().ImplementInterface(typeof(IAuthService))
            .Should().ResideInNamespaceStartingWith("ReturnLoad.Infrastructure")
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            "IAuthService implementations must live in ReturnLoad.Infrastructure. Offending: "
            + string.Join(", ", result.FailingTypeNames ?? new List<string>()));
    }
}
