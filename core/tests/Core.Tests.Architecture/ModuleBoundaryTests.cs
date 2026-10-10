using System.Reflection;

namespace Core.Tests.Architecture;

/// <summary>Issue 24: modules see each other only through contracts and the platform.</summary>
public sealed class ModuleBoundaryTests
{
    [Fact]
    public void NoModuleReferencesAnotherModuleImplementation()
    {
        var violations = ArchitectureRules.ProductionAssemblies()
            .SelectMany(ArchitectureRules.ModuleBoundaryViolations)
            .ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void RuleFindsAModuleThatReferencesAnotherImplementation()
    {
        var fixture = Assembly.Load(new AssemblyName("Core.FixtureViolation"));

        var violations = ArchitectureRules.ModuleBoundaryViolations(fixture);

        Assert.Equal(["Core.FixtureViolation references Core.FixtureModule"], violations);
    }
}
