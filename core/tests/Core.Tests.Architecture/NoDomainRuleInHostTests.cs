using System.Reflection;

namespace Core.Tests.Architecture;

/// <summary>
/// Issue 24 CA-3: the host composes and the platform serves; neither names an annotation category,
/// a form of material or a pipeline step.
/// </summary>
public sealed class NoDomainRuleInHostTests
{
    [Theory]
    [InlineData(ArchitectureRules.Host)]
    [InlineData(ArchitectureRules.Platform)]
    public void NoDomainTermInAnyDeclaredIdentifier(string assemblyName)
    {
        var assembly = Assembly.Load(new AssemblyName(assemblyName));

        var violations = ArchitectureRules.DomainTermViolations(assembly);

        Assert.Empty(violations);
    }

    [Fact]
    public void RuleFindsDomainTermsInTypesAndMembers()
    {
        var fixture = Assembly.Load(new AssemblyName("Core.FixtureViolation"));

        var violations = ArchitectureRules.DomainTermViolations(fixture);

        Assert.Contains("Core.FixtureViolation: 'ConversationExporter' contains 'conversation'", violations);
        Assert.Contains("Core.FixtureViolation: 'ConversationExporter' contains 'export'", violations);
        Assert.Contains("Core.FixtureViolation: 'CountAnnotationCategories' contains 'annotation'", violations);
    }
}
