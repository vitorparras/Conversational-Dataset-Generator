using System.Net;
using System.Text.Json;
using Core.Persistence.People;
using Core.Persistence.Tests.Fixtures;

namespace Core.Tests.Integration.Hosts;

/// <summary>
/// The conference of the guarantees of the server at the built host over a real database (issue #68, CA-1, CA-2 and CA-5): one runs when the host comes up and the route shows what it conferred and
/// when; a guarantee removed is told in the route and in the log by name while the chain still closes; a conference that cannot be recorded leaves the route saying "late", not silent.
/// </summary>
[Trait("Category", "Database")]
public sealed class GuaranteeConferenceHostTests
{
    private const string Route = "/custody/verification";
    private const string OneSecond = "--Custody:VerificationInterval=00:00:01";
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(60);

    [DatabaseFact]
    public async Task ConferenceRunsWhenTheHostComesUpAndTheRouteShowsWhatItConferredAndWhen()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await AuthenticatedHost.StartAsync(new HostSetup { Function = PersonFunctions.Administration }, cancellationToken);

        var shown = await UntilAsync(host, body => body.GetProperty("guarantees").GetProperty("passed").ValueKind == JsonValueKind.True, cancellationToken);

        var guarantees = shown.GetProperty("guarantees");
        Assert.Equal(
            ["Privilege", "RowPolicy", "ColumnRevoke", "Immutability", "ProtectedColumns", "EncryptedSample"],
            guarantees.GetProperty("checked").EnumerateArray().Select(name => name.GetString()));
        Assert.Empty(guarantees.GetProperty("missing").EnumerateArray());
        Assert.InRange(guarantees.GetProperty("recordedAt").GetDateTimeOffset(), DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddMinutes(1));
        Assert.False(guarantees.GetProperty("overdue").GetBoolean());
    }

    [DatabaseFact]
    public async Task GuaranteeRemovedIsToldByNameInTheRouteAndInTheLogWhileTheChainStillCloses()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await AuthenticatedHost.StartAsync(new HostSetup { Function = PersonFunctions.Administration, Arguments = [OneSecond], WithLogFile = true }, cancellationToken);
        _ = await UntilAsync(host, body => body.GetProperty("guarantees").GetProperty("passed").ValueKind == JsonValueKind.True, cancellationToken);

        await host.RunAsSuperuserAsync("ALTER TABLE content.item_text NO FORCE ROW LEVEL SECURITY", cancellationToken);
        var shown = await UntilAsync(host, body => body.GetProperty("guarantees").GetProperty("passed").ValueKind == JsonValueKind.False, cancellationToken);

        Assert.Equal(["RowPolicy"], shown.GetProperty("guarantees").GetProperty("missing").EnumerateArray().Select(name => name.GetString()));
        Assert.True(shown.GetProperty("last").GetProperty("passed").GetBoolean());
        await LogHasAsync(host, "FALTAM RowPolicy", cancellationToken);
    }

    [DatabaseFact]
    public async Task ConferenceThatCannotBeRecordedLeavesTheRouteSayingLateAndIsToldInTheLog()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await AuthenticatedHost.StartAsync(new HostSetup { Function = PersonFunctions.Administration, Arguments = [OneSecond], WithLogFile = true }, cancellationToken);
        _ = await UntilAsync(host, body => body.GetProperty("guarantees").GetProperty("recordedAt").ValueKind == JsonValueKind.String, cancellationToken);

        await host.RunAsSuperuserAsync(
            "CREATE FUNCTION public.zqx_refuse() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'zqx'; END $$; "
            + "CREATE TRIGGER zqx_refuse BEFORE INSERT ON public.custody_event FOR EACH ROW EXECUTE FUNCTION public.zqx_refuse()",
            cancellationToken);
        await LogHasAsync(host, "Conferência das garantias do servidor não chegou ao fim: InvalidOperationException", cancellationToken);
        var stopped = await UntilAsync(host, body => body.GetProperty("guarantees").GetProperty("recordedAt").ValueKind == JsonValueKind.String, cancellationToken);
        var stoppedAt = stopped.GetProperty("guarantees").GetProperty("recordedAt").GetDateTimeOffset();
        var late = await UntilAsync(host, body => body.GetProperty("guarantees").GetProperty("overdue").GetBoolean(), cancellationToken);
        using var alive = await host.GetAsync("/status", cancellationToken);

        Assert.Equal(stoppedAt, late.GetProperty("guarantees").GetProperty("recordedAt").GetDateTimeOffset());
        Assert.Equal(HttpStatusCode.OK, alive.StatusCode);
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
