using Core.Tests.Architecture.CodeRules;

namespace Core.Tests.Architecture.Boundaries;

/// <summary>The host holds composition, endpoints and supervision only, as docs/operacao/areas/Core.Host.md describes it, with fixtures that name the domain.</summary>
public sealed class NoDomainRuleInHostTests
{
    private const string Fixtures = "host-domain";

    [Fact]
    public void HostDeclaresNoNameFromTheDomain()
    {
        var violations = NoDomainRuleInHostRules.Check(NoDomainRuleInHostRules.HostFiles());

        Assert.Empty(violations);
    }

    [Fact]
    public void HostCompositionEndpointsAndSupervisionAreAmongTheJudgedNames()
    {
        var names = NoDomainRuleInHostRules.DeclaredNames(NoDomainRuleInHostRules.HostFiles()).ToList();

        Assert.Contains("CompositionRoot", names);
        Assert.Contains("StatusEndpoint", names);
        Assert.Contains("LanguageServiceSupervisor", names);
        Assert.Contains("SupervisionLogMessages", names);
    }

    [Fact]
    public void DomainTermInADeclaredNameIsRefused()
    {
        var fixture = SourceTree.Fixture(Path.Combine(Fixtures, "DomainNamesInHost.cs"));

        var lines = NoDomainRuleInHostRules.Check([fixture]).Select(violation => violation.Line).Distinct().Order();

        Assert.Equal([1, 3, 5, 7, 9, 11, 13, 18, 19, 22, 26, 28, 33, 38, 40], lines);
    }

    [Fact]
    public void SupervisionEndpointLogMessageAndRecognizerCompositionNamesAreAccepted()
    {
        var fixture = SourceTree.Fixture(Path.Combine(Fixtures, "SupervisionOnly.cs"));

        var violations = NoDomainRuleInHostRules.Check([fixture]);

        Assert.Empty(violations);
    }

    [Fact]
    public void DomainTermInAFileNameIsRefused()
    {
        var fixture = SourceTree.Fixture(Path.Combine(Fixtures, "AnnotationQueue.cs"));

        var violation = Assert.Single(NoDomainRuleInHostRules.Check([fixture]));

        Assert.Contains("file name AnnotationQueue.cs", violation.Message, StringComparison.Ordinal);
    }
}
