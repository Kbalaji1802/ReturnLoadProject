using NetArchTest.Rules;

namespace ReturnLoad.ArchitectureTests;

/// <summary>
/// Executable guardrails for the Clean Architecture dependency rule: dependencies
/// only ever point inward. Domain is the core and depends on nothing; Application
/// depends only on Domain; Infrastructure and API sit on the outside.
/// <para>
/// These tests inspect the compiled IL of each layer, so they catch a violation
/// the moment code actually uses a forbidden type — no reviewer vigilance required.
/// If one fails, the fix is almost never to change the test: it is to remove the
/// inward-pointing reference (see ai/03_TECHNICAL_BIBLE.md).
/// </para>
/// </summary>
public sealed class LayerDependencyTests
{
    private const string Domain = "ReturnLoad.Domain";
    private const string Application = "ReturnLoad.Application";
    private const string Infrastructure = "ReturnLoad.Infrastructure";
    private const string Api = "ReturnLoad.Api";

    // Anchor types used only to resolve each layer's compiled assembly.
    private static System.Reflection.Assembly DomainAssembly => typeof(ReturnLoad.Domain.Common.BaseEntity<>).Assembly;
    private static System.Reflection.Assembly ApplicationAssembly => typeof(ReturnLoad.Application.IApplicationMarker).Assembly;
    private static System.Reflection.Assembly InfrastructureAssembly => typeof(ReturnLoad.Infrastructure.DependencyInjection).Assembly;

    [Fact]
    public void Domain_should_not_depend_on_any_outer_layer()
    {
        TestResult result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(Application, Infrastructure, Api)
            .GetResult();

        AssertArchitecture(result, "Domain must depend on nothing outside itself");
    }

    [Fact]
    public void Application_should_not_depend_on_Infrastructure_or_Api()
    {
        TestResult result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(Infrastructure, Api)
            .GetResult();

        AssertArchitecture(result, "Application may depend on Domain only, never outward");
    }

    [Fact]
    public void Infrastructure_should_not_depend_on_Api()
    {
        TestResult result = Types.InAssembly(InfrastructureAssembly)
            .ShouldNot()
            .HaveDependencyOn(Api)
            .GetResult();

        AssertArchitecture(result, "Infrastructure is an outer layer but must not reference the API host");
    }

    private static void AssertArchitecture(TestResult result, string rule)
    {
        Assert.True(
            result.IsSuccessful,
            $"Clean Architecture violation — {rule}.{System.Environment.NewLine}"
            + "Offending type(s): "
            + string.Join(", ", result.FailingTypeNames ?? new List<string>()));
    }
}
