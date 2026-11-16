using Core.Tests.Architecture.CodeRules;

namespace Core.Tests.Architecture.Boundaries;

/// <summary>
/// Only the reader of the request reads the form and resolves the person of the session by the cookie in one step (issue #817), with fixtures that break each part.
/// </summary>
public sealed class RequestReaderTests
{
    private const string Fixtures = "request-reader";

    [Fact]
    public void HostReadsTheFormAndTheSessionOnlyThroughTheReader()
    {
        var violations = RequestReaderRules.Check(NoDomainRuleInHostRules.HostFiles());

        Assert.Empty(violations);
    }

    [Fact]
    public void ReadingTheFormAndResolvingTheSessionByHandAreRefused()
    {
        var fixture = SourceTree.Fixture(Path.Combine(Fixtures, "ReadsByHand.cs"));

        var violations = RequestReaderRules.Check([fixture]).ToList();

        Assert.Equal([7, 8, 9, 10], violations.Select(violation => violation.Line));
    }

    [Fact]
    public void TheReaderItselfAndTheRoutesThatGoThroughItAreAccepted()
    {
        var reader = SourceTree.Fixture(Path.Combine(Fixtures, "RequestReader.cs"));
        var route = SourceTree.Fixture(Path.Combine(Fixtures, "ThroughTheReader.cs"));

        var violations = RequestReaderRules.Check([reader, route]);

        Assert.Empty(violations);
    }
}
