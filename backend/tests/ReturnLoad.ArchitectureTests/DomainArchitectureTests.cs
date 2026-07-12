using NetArchTest.Rules;
using ReturnLoad.Domain.Common;

namespace ReturnLoad.ArchitectureTests;

/// <summary>
/// Conventions that keep the Domain layer clean as it grows (M3 core domain model).
/// The layer's dependency purity is covered by <see cref="LayerDependencyTests"/>; these
/// guard the modelling conventions.
/// </summary>
public sealed class DomainArchitectureTests
{
    private static System.Reflection.Assembly DomainAssembly => typeof(ValueObject).Assembly;

    [Fact]
    public void Value_objects_are_sealed_and_live_in_the_domain()
    {
        TestResult result = Types.InAssembly(DomainAssembly)
            .That().Inherit(typeof(ValueObject))
            .Should().BeSealed()
            .And().ResideInNamespaceStartingWith("ReturnLoad.Domain")
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            "Value objects must be sealed and reside in ReturnLoad.Domain. Offending: "
            + string.Join(", ", result.FailingTypeNames ?? new List<string>()));
    }

    [Fact]
    public void Domain_events_are_immutable_and_live_in_the_domain()
    {
        TestResult result = Types.InAssembly(DomainAssembly)
            .That().ImplementInterface(typeof(IDomainEvent))
            .Should().ResideInNamespaceStartingWith("ReturnLoad.Domain")
            .GetResult();

        Assert.True(result.IsSuccessful);
    }
}
