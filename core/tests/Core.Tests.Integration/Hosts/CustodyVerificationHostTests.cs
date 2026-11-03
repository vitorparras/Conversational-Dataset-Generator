using System.Net;
using System.Text.Json;
using Core.Persistence.Tests.Fixtures;

namespace Core.Tests.Integration.Hosts;

/// <summary>
/// The verification of the chain at the built host over a real database (issue #61, part 2, CA-2 and CA-4 to CA-6): one runs when the host comes up and is recorded in the chain, a broken
/// chain does not stop the host nor its reading and is told in the log and in the route, a verification that cannot be recorded is told in the log and does not take the host down,
/// and the route says "late" when none was recorded or the last is too old.
/// </summary>
[Trait("Category", "Database")]
public sealed class CustodyVerificationHostTests
{
    private const string Route = "/custody/verification";
    private const string OneSecond = "--Custody:VerificationInterval=00:00:01";
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(60);

    [DatabaseFact]
    public async Task VerificationRunsWhenTheHostComesUpWithNobodyAskingAndTheRouteShowsItRecordedWithMomentCountAndInterval()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await AuthenticatedHost.StartAsync(new HostSetup(), cancellationToken);

        var shown = await UntilAsync(host, body => body.GetProperty("last").ValueKind == JsonValueKind.Object, cancellationToken);

        var last = shown.GetProperty("last");
        Assert.True(last.GetProperty("passed").GetBoolean());
        var firstVerification = await host.NumberAsSuperuserAsync("SELECT min(sequence) FROM public.custody_event WHERE step = 'verify_chain'", cancellationToken);
        Assert.Equal(firstVerification - 1, last.GetProperty("eventsRead").GetInt64());
        Assert.Equal(JsonValueKind.Null, last.GetProperty("firstInconsistent").ValueKind);
        Assert.InRange(last.GetProperty("recordedAt").GetDateTimeOffset(), DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddMinutes(1));
        Assert.Equal("00:05:00", shown.GetProperty("interval").GetString());
        Assert.False(shown.GetProperty("overdue").GetBoolean());
        Assert.Equal(["guarantees", "interval", "last", "overdue"], shown.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
    }

    [DatabaseFact]
    public async Task IntervalInForceIsShownByTheConfigurationRoute()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await AuthenticatedHost.StartAsync(new HostSetup(), cancellationToken);

        using var response = await host.GetAsync("/configuration", cancellationToken);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));

        var entry = body.RootElement.GetProperty("entries").EnumerateArray().Single(item => item.GetProperty("key").GetString() == "Custody:VerificationInterval");
        Assert.Equal("00:05:00", entry.GetProperty("value").GetString());
    }

    [DatabaseFact]
    public async Task BrokenChainDoesNotStopTheHostNorItsReadingAndIsToldInTheLogAndInTheRoute()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await AuthenticatedHost.StartAsync(new HostSetup { Arguments = [OneSecond], WithLogFile = true }, cancellationToken);
        _ = await UntilAsync(host, body => body.GetProperty("last").ValueKind == JsonValueKind.Object, cancellationToken);

        await host.RunAsSuperuserAsync(
            "ALTER TABLE public.custody_event DISABLE TRIGGER custody_event_never_changed; UPDATE public.custody_event SET item_id = item_id + 1 WHERE sequence = 1; "
            + "ALTER TABLE public.custody_event ENABLE ALWAYS TRIGGER custody_event_never_changed",
            cancellationToken);
        var shown = await UntilAsync(host, body => !body.GetProperty("last").GetProperty("passed").GetBoolean(), cancellationToken);

        Assert.Equal(1, shown.GetProperty("last").GetProperty("firstInconsistent").GetInt64());
        await LogHasAsync(host, "QUEBRADA no evento de sequência 1 (DigestMismatch)", cancellationToken);
        using var status = await host.GetAsync("/status", cancellationToken);
        using var session = await host.GetAsync("/session", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, status.StatusCode);
        Assert.Contains("\"ready\":true", await status.Content.ReadAsStringAsync(cancellationToken), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.NoContent, session.StatusCode);
    }

    [DatabaseFact]
    public async Task VerificationThatCannotBeRecordedIsLoggedTheRouteSaysLateAndTheNextOneRecordsOnceItCanAndTheHostStaysUp()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await AuthenticatedHost.StartAsync(new HostSetup { Arguments = [OneSecond], WithLogFile = true }, cancellationToken);
        _ = await UntilAsync(host, body => body.GetProperty("last").ValueKind == JsonValueKind.Object, cancellationToken);

        await host.RunAsSuperuserAsync(
            "CREATE FUNCTION public.zqx_refuse() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'zqx'; END $$; "
            + "CREATE TRIGGER zqx_refuse BEFORE INSERT ON public.custody_event FOR EACH ROW EXECUTE FUNCTION public.zqx_refuse()",
            cancellationToken);
        await LogHasAsync(host, "não chegou ao fim: InvalidOperationException", cancellationToken);
        var stopped = await UntilAsync(host, body => body.GetProperty("last").ValueKind == JsonValueKind.Object, cancellationToken);
        var stoppedAt = stopped.GetProperty("last").GetProperty("recordedAt").GetDateTimeOffset();
        _ = await UntilAsync(host, body => body.GetProperty("overdue").GetBoolean(), cancellationToken);
        using var alive = await host.GetAsync("/status", cancellationToken);
        var stillLate = await UntilAsync(host, _ => true, cancellationToken);
        await host.RunAsSuperuserAsync("DROP TRIGGER zqx_refuse ON public.custody_event; DROP FUNCTION public.zqx_refuse()", cancellationToken);
        var caughtUp = await UntilAsync(host, body => body.GetProperty("last").GetProperty("recordedAt").GetDateTimeOffset() > stoppedAt, cancellationToken);

        Assert.Equal(stoppedAt, stillLate.GetProperty("last").GetProperty("recordedAt").GetDateTimeOffset());
        Assert.True(stillLate.GetProperty("overdue").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, alive.StatusCode);
        Assert.True(caughtUp.GetProperty("last").GetProperty("passed").GetBoolean());
    }

    private static async Task<JsonElement> UntilAsync(AuthenticatedHost host, Func<JsonElement, bool> reached, CancellationToken cancellationToken)
    {
        var until = DateTimeOffset.UtcNow + Patience;
        while (true)
        {
            using var response = await host.GetAsync(Route, cancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            if (reached(body.RootElement))
            {
                return body.RootElement.Clone();
            }

            Assert.True(DateTimeOffset.UtcNow < until, $"The route never showed what the test waits for; the last answer was {body.RootElement}.");
            await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);
        }
    }

    private static async Task LogHasAsync(AuthenticatedHost host, string text, CancellationToken cancellationToken)
    {
        var until = DateTimeOffset.UtcNow + Patience;
        while (!host.LogText.Contains(text, StringComparison.Ordinal))
        {
            Assert.True(DateTimeOffset.UtcNow < until, $"The log never held '{text}'; it holds: {host.LogText}");
            await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);
        }
    }
}
