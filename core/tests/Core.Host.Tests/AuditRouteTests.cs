using System.Net;
using System.Text.Json;

namespace Core.Host.Tests;

/// <summary>
/// The route that lists what the server's audit log holds is one of a live session, refuses an interval that is not two instants in order,
/// answers 503 when the log cannot be read, and carries no text of any statement (issue #60, part 2).
/// </summary>
public sealed class AuditRouteTests
{
    private static readonly DateTimeOffset Noon = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    private const string Interval = "?from=2026-11-01T12:00:00Z&to=2026-11-01T13:00:00Z";

    [Fact]
    public async Task RequestWithoutASessionGets401PointingAtTheEntry()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await AuditHost.StartAsync(cancellationToken);

        using var response = await host.GetAsync(Interval, withSession: false, cancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ListingOfTheIntervalHasTheRoleTheClassTheCommandAndTheObjectAndNoTextOfAStatement()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await AuditHost.StartAsync(cancellationToken);
        host.WriteLog(Noon.AddMinutes(1), "INSERT", "UPDATE");

        using var response = await host.GetAsync(Interval, withSession: true, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("zqx-text", body, StringComparison.Ordinal);
        using var listed = JsonDocument.Parse(body);
        var entries = listed.RootElement.GetProperty("entries").EnumerateArray().ToArray();
        Assert.Equal(["INSERT", "UPDATE"], entries.Select(entry => entry.GetProperty("command").GetString()));
        Assert.Equal(("app", "WRITE", "public.person"), (Text(entries[0], "role"), Text(entries[0], "class"), Text(entries[0], "objectName")));
    }

    [Theory]
    [InlineData("")]
    [InlineData("?from=2026-11-01T12:00:00Z")]
    [InlineData("?from=2026-11-01T13:00:00Z&to=2026-11-01T12:00:00Z")]
    [InlineData("?from=2026-11-01T12:00:00Z&to=2026-11-01T12:00:00Z")]
    [InlineData("?from=2026-11-01T12:00:00&to=2026-11-01T13:00:00")]
    [InlineData("?from=ontem&to=hoje")]
    public async Task IntervalThatIsNotTwoInstantsWithAZoneInOrderIsRefusedWith400AndWhatIsAccepted(string query)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await AuditHost.StartAsync(cancellationToken);

        using var response = await host.GetAsync(query, withSession: true, cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("from e to", await response.Content.ReadAsStringAsync(cancellationToken), StringComparison.Ordinal);
    }

    [Fact]
    public async Task IntervalWiderThanAWeekIsRefusedWith400AndTheMostItAccepts()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await AuditHost.StartAsync(cancellationToken);

        using var response = await host.GetAsync("?from=2026-11-01T12:00:00Z&to=2026-11-08T12:00:01Z", withSession: true, cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("7 dias", await response.Content.ReadAsStringAsync(cancellationToken), StringComparison.Ordinal);
    }

    [Fact]
    public async Task IntervalOfExactlyAWeekIsAccepted()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await AuditHost.StartAsync(cancellationToken);

        using var response = await host.GetAsync("?from=2026-11-01T12:00:00Z&to=2026-11-08T12:00:00Z", withSession: true, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task IntervalWithMoreLinesThanAnAnswerCarriesIsRefusedWith400AndTheMostItAccepts()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await AuditHost.StartAsync(cancellationToken);
        host.WriteLog(Noon.AddMinutes(1), [.. Enumerable.Repeat("INSERT", 10_001)]);

        using var response = await host.GetAsync("?from=2026-11-01T12:00:00Z&to=2026-11-03T12:00:00Z", withSession: true, cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("10000 linhas", await response.Content.ReadAsStringAsync(cancellationToken), StringComparison.Ordinal);
    }

    [Fact]
    public async Task LogThatCannotBeReadAnswers503WithoutSayingWhy()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await AuditHost.StartAsync(cancellationToken);
        host.WriteLog(AuditHost.AuditLine(Noon.AddMinutes(1), "INSERT", zone: "-03"));

        using var response = await host.GetAsync(Interval, withSession: true, cancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.DoesNotContain("-03", await response.Content.ReadAsStringAsync(cancellationToken), StringComparison.Ordinal);
    }

    private static string? Text(JsonElement entry, string name) => entry.GetProperty(name).GetString();
}
