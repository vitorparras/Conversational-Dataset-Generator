using Core.Host.Endpoints;
using Core.Persistence.Ingestion;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Core.Host.Tests.Submissions;

/// <summary>
/// What the address of the list may ask for: a page that skips more than the register reads, or holds more than a page may, is a parameter that is not accepted, named by the field (issue 75).
/// </summary>
public sealed class BatchQueryTests
{
    private static string Fields(params (string Name, string Value)[] parameters) =>
        string.Join(',', BatchQuery.Problems(Query(parameters)).Select(problem => problem.Field));

    private static QueryCollection Query((string Name, string Value)[] parameters) =>
        new(parameters.ToDictionary(parameter => parameter.Name, parameter => new StringValues(parameter.Value), StringComparer.Ordinal));

    [Theory]
    [InlineData("101", "1", "pageSize")]
    [InlineData("0", "1", "pageSize")]
    [InlineData("100", "102", "page")]
    [InlineData("1", "2147483647", "page")]
    [InlineData("100", "0", "page")]
    public void PageThatSkipsTooMuchOrHoldsTooMuchIsAParameterThatIsNotAccepted(string pageSize, string page, string field) =>
        Assert.Equal(field, Fields(("pageSize", pageSize), ("page", page)));

    [Theory]
    [InlineData("100", "101")]
    [InlineData("50", "1")]
    [InlineData("1", "10001")]
    public void PageWithinTheLimitsIsAccepted(string pageSize, string page)
    {
        Assert.Equal(string.Empty, Fields(("pageSize", pageSize), ("page", page)));
        Assert.NotNull(BatchQuery.Of(Query([("pageSize", pageSize), ("page", page)]), [1]));
    }

    [Theory]
    [InlineData("open", BatchState.Open)]
    [InlineData("accepted", BatchState.Accepted)]
    [InlineData("Rejected", BatchState.Rejected)]
    public void StateIsAcceptedOnlyByOneOfTheThreeNames(string name, BatchState state)
    {
        Assert.Equal(string.Empty, Fields(("state", name)));
        Assert.Equal(state, BatchQuery.Of(Query([("state", name)]), [1])!.Filter.State);
    }

    [Theory]
    [InlineData("7")]
    [InlineData("-1")]
    [InlineData("open,accepted")]
    [InlineData("open, rejected")]
    [InlineData("")]
    [InlineData("zqx")]
    public void StateThatIsNotOneOfTheThreeNamesIsAParameterThatIsNotAcceptedAndNothingIsListed(string state)
    {
        Assert.Equal("state", Fields(("state", state)));
        Assert.Null(BatchQuery.Of(Query([("state", state)]), [1]));
    }

    [Theory]
    [InlineData("2026-11-01T12:00:00Z")]
    [InlineData("2026-11-01T12:00:00.123Z")]
    [InlineData("2026-11-01T09:00:00-03:00")]
    [InlineData("2026-11-01")]
    public void MomentInIso8601IsAccepted(string moment) =>
        Assert.Equal(string.Empty, Fields(("from", moment), ("until", moment)));

    [Theory]
    [InlineData("ontem")]
    [InlineData("11/01/2026")]
    [InlineData("1 nov 2026")]
    [InlineData("2026-11-01 12:00")]
    [InlineData("1762000000")]
    public void MomentThatIsNotInIso8601IsAParameterThatIsNotAccepted(string moment) =>
        Assert.Equal("from,until", Fields(("from", moment), ("until", moment)));

    [Fact]
    public void LastPageTheRegisterReadsSkipsExactlyTheMostItSkips()
    {
        var asked = BatchQuery.Of(Query([("pageSize", "100"), ("page", "101")]), [1]);

        Assert.Equal(BatchRegister.MostSkipped, asked!.Filter.Skip);
    }
}
