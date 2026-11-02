using Core.Tests.Architecture.CodeRules;

namespace Core.Tests.Architecture.Boundaries;

/// <summary>Nothing in the host answers around the session gate and only the entry and the status are marked open (issue #495, CA-3), with fixtures that break each part.</summary>
public sealed class SessionGateBypassTests
{
    private const string Fixtures = "session-gate";

    [Fact]
    public void HostServesNoRequestAroundTheGateAndMarksOpenOnlyTheEntryAndTheStatus()
    {
        var violations = SessionGateBypassRules.Check(NoDomainRuleInHostRules.HostFiles());

        Assert.Empty(violations);
    }

    [Fact]
    public void ShortCircuitBranchesAndTerminalMiddlewareAreRefused()
    {
        var fixture = SourceTree.Fixture(Path.Combine(Fixtures, "AroundTheGate.cs"));

        var violations = SessionGateBypassRules.Check([fixture]).ToList();

        Assert.Equal([7, 8, 9, 10, 11, 12, 13, 14, 15], violations.Select(violation => violation.Line));
    }

    [Fact]
    public void RoutesMappedByTheHostsOwnClassesAreAccepted()
    {
        var fixture = SourceTree.Fixture(Path.Combine(Fixtures, "BehindTheGate.cs"));

        var violations = SessionGateBypassRules.Check([fixture]);

        Assert.Empty(violations);
    }

    [Fact]
    public void OpenMarkInAFileOtherThanTheEntryAndTheStatusIsRefused()
    {
        var fixture = SourceTree.Fixture(Path.Combine(Fixtures, "OpenElsewhere.cs"));

        var violation = Assert.Single(SessionGateBypassRules.Check([fixture]));

        Assert.Equal(7, violation.Line);
        Assert.Contains("SessionEndpoint.cs and StatusEndpoint.cs", violation.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OpenMarkInTheFilesOfTheEntryAndTheStatusIsAccepted()
    {
        var entry = SourceTree.Fixture(Path.Combine(Fixtures, "SessionEndpoint.cs"));
        var status = SourceTree.Fixture(Path.Combine(Fixtures, "StatusEndpoint.cs"));

        var violations = SessionGateBypassRules.Check([entry, status]);

        Assert.Empty(violations);
    }
}
