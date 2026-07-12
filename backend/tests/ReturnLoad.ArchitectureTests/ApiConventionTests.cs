using Microsoft.AspNetCore.Mvc;
using NetArchTest.Rules;

namespace ReturnLoad.ArchitectureTests;

/// <summary>
/// Conventions that keep the API surface consistent as it grows. Controllers are the
/// public face of the platform; keeping them uniformly organised makes routing,
/// discovery, and review predictable.
/// </summary>
public sealed class ApiConventionTests
{
    private static System.Reflection.Assembly ApiAssembly => typeof(Program).Assembly;

    [Fact]
    public void Controllers_reside_in_the_Controllers_namespace()
    {
        TestResult result = Types.InAssembly(ApiAssembly)
            .That().Inherit(typeof(ControllerBase))
            .Should().ResideInNamespaceStartingWith("ReturnLoad.Api.Controllers")
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            "All controllers must live under ReturnLoad.Api.Controllers. Offending: "
            + string.Join(", ", result.FailingTypeNames ?? new List<string>()));
    }
}
