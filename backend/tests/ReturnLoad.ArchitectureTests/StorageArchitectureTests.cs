using NetArchTest.Rules;
using ReturnLoad.Application.Abstractions.Storage;

namespace ReturnLoad.ArchitectureTests;

/// <summary>
/// Guards the file-storage seam (ADR-0012): the abstraction lives in Application so
/// business code depends inward; concrete providers live in Infrastructure so the
/// backend can be swapped without touching business code.
/// </summary>
public sealed class StorageArchitectureTests
{
    private static System.Reflection.Assembly InfrastructureAssembly =>
        typeof(ReturnLoad.Infrastructure.DependencyInjection).Assembly;

    [Fact]
    public void Storage_abstraction_lives_in_the_Application_layer()
    {
        Assert.StartsWith("ReturnLoad.Application", typeof(IFileStorageService).Namespace);
    }

    [Fact]
    public void Storage_implementations_live_in_the_Infrastructure_layer()
    {
        TestResult result = Types.InAssembly(InfrastructureAssembly)
            .That().ImplementInterface(typeof(IFileStorageService))
            .Should().ResideInNamespaceStartingWith("ReturnLoad.Infrastructure")
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            "IFileStorageService implementations must live in ReturnLoad.Infrastructure. Offending: "
            + string.Join(", ", result.FailingTypeNames ?? new List<string>()));
    }
}
