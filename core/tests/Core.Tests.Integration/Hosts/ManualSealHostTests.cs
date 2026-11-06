using System.Net;
using System.Text.Json;
using Core.Persistence.Custody.Verification;
using Core.Persistence.People;
using Core.Persistence.Tests.Fixtures;

namespace Core.Tests.Integration.Hosts;

/// <summary>
/// The seal of the manual situation at the built host over a real database (issue #62, part B2b): the route that issues the seal writes nothing, the route that confirms it records the event
/// in the name of the person of the session and only for a seal the chain has, a seal altered or of a sequence the chain does not have is 409 with the reason and nothing written, the two
/// routes refuse the situations that send the seal themselves, a body above the limit is 413 on both routes that receive a seal, and the status carries the warning of the manual situation
/// only (the ages of 8 and 6 days are in SealAgeTests and SealWarningTests).
/// </summary>
[Trait("Category", "Database")]
public sealed class ManualSealHostTests
{
    private const string Issue = "/custody/seal";
    private const string Confirm = "/custody/seal/confirmation";
    private const string Verify = "/custody/verification";
    private const string Count = "SELECT count(*) FROM public.custody_event";
    private const string Recorded = "SELECT count(*) FROM public.custody_event WHERE step = 'seal_recorded'";
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(60);

    [DatabaseFact]
    public async Task TheSealIssuedThenConfirmedRecordsTheEventInTheNameOfThePersonAndTheChainVerifiesAndTheSealHolds()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await StartedAsync(new HostSetup(), cancellationToken);
        var before = await host.NumberAsSuperuserAsync(Count, cancellationToken);

        var issued = await IssueAsync(host, cancellationToken);
        var afterIssue = await host.NumberAsSuperuserAsync(Count, cancellationToken);
        using var confirmed = await host.PostAsync(Confirm, Body(issued), cancellationToken);

        Assert.Equal(before, afterIssue);
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        Assert.Equal(1L, await host.NumberAsSuperuserAsync(Recorded, cancellationToken));
        Assert.Equal(
            1L,
            await host.NumberAsSuperuserAsync(
                "SELECT count(*) FROM public.custody_event e JOIN public.person p ON p.id = e.actor_person_id WHERE e.step = 'seal_recorded' AND NOT e.actor_system", cancellationToken));
        var shown = await VerifyAsync(host, issued, cancellationToken);
        Assert.Equal((true, true), (shown.GetProperty("passed").GetBoolean(), shown.GetProperty("seal").GetProperty("holds").GetBoolean()));
    }

    [DatabaseFact]
    public async Task ASealAlteredIsRefusedWith409AndTheReasonAndNothingIsWritten()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await StartedAsync(new HostSetup(), cancellationToken);
        var altered = ChainSeal.Of(DateTimeOffset.UtcNow, ChainSealCount(await IssueAsync(host, cancellationToken)), new byte[32]).ToText();
        var before = await host.NumberAsSuperuserAsync(Count, cancellationToken);

        using var refused = await host.PostAsync(Confirm, Body(altered), cancellationToken);

        await AssertRefusedAsync(refused, cancellationToken);
        Assert.Equal(before, await host.NumberAsSuperuserAsync(Count, cancellationToken));
    }

    [DatabaseFact]
    public async Task ASealOfASequenceTheChainDoesNotHaveIsRefusedWith409AndTheReasonAndNothingIsWritten()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await StartedAsync(new HostSetup(), cancellationToken);
        var last = await host.NumberAsSuperuserAsync("SELECT max(sequence) FROM public.custody_event", cancellationToken);
        var before = await host.NumberAsSuperuserAsync(Count, cancellationToken);

        using var refused = await host.PostAsync(Confirm, Body(ChainSeal.Of(DateTimeOffset.UtcNow, last + 500, new byte[32]).ToText()), cancellationToken);

        await AssertRefusedAsync(refused, cancellationToken);
        Assert.Equal(before, await host.NumberAsSuperuserAsync(Count, cancellationToken));
    }

    [DatabaseTheory]
    [InlineData("")]
    [InlineData("zqx")]
    public async Task ATextThatIsNotASealIsRefusedWith400AndNothingIsWritten(string text)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await StartedAsync(new HostSetup(), cancellationToken);
        var before = await host.NumberAsSuperuserAsync(Count, cancellationToken);

        using var refused = await host.PostAsync(Confirm, Body(text), cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Equal(before, await host.NumberAsSuperuserAsync(Count, cancellationToken));
    }

    [DatabaseTheory]
    [InlineData("External")]
    [InlineData("Internal")]
    public async Task TheSituationsThatSendTheSealThemselvesRefuseBothRoutesWith409AndWriteNothing(string situation)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var setup = new HostSetup { Arguments = [$"--Custody:SealSituation={situation}", "--Custody:SealDestination=https://zqx-vault.example.test/selos"] };
        await using var host = await StartedAsync(setup, cancellationToken);
        var before = await host.NumberAsSuperuserAsync(Count, cancellationToken);

        using var issue = await host.GetAsync(Issue, cancellationToken);
        using var confirm = await host.PostAsync(Confirm, Body(ChainSeal.Of(DateTimeOffset.UtcNow, 1, new byte[32]).ToText()), cancellationToken);

        Assert.Equal((HttpStatusCode.Conflict, HttpStatusCode.Conflict), (issue.StatusCode, confirm.StatusCode));
        Assert.Equal(before, await host.NumberAsSuperuserAsync(Count, cancellationToken));
    }

    [DatabaseTheory]
    [InlineData(Confirm)]
    [InlineData(Verify)]
    public async Task ABodyAboveTheLimitIsRefusedWith413OnBothRoutesThatReceiveASeal(string route)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await StartedAsync(new HostSetup(), cancellationToken);
        var before = await host.NumberAsSuperuserAsync(Count, cancellationToken);

        using var refused = await host.PostAsync(route, Body(new string('z', 4096)), cancellationToken);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, refused.StatusCode);
        Assert.Equal(before, await host.NumberAsSuperuserAsync(Count, cancellationToken));
    }

    [DatabaseFact]
    public async Task TheVerificationWarnsWhenNoSealWasEverRecordedAndStopsWarningOnceOneIsRecorded()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await StartedAsync(new HostSetup(), cancellationToken);
        var none = await ReadAsync(host, cancellationToken);
        using var confirmed = await host.PostAsync(Confirm, Body(await IssueAsync(host, cancellationToken)), cancellationToken);
        var recent = await ReadAsync(host, cancellationToken);

        Assert.Contains("Nenhum selo", none.GetProperty("warning").GetString(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        Assert.Equal(JsonValueKind.Null, recent.GetProperty("warning").ValueKind);
    }

    private static long ChainSealCount(string seal) => ChainSeal.Read(seal).Seal!.Events;

    [DatabaseTheory]
    [InlineData("Manual", true)]
    [InlineData("External", false)]
    public async Task TheStatusCarriesOnlyTheWarningOfTheManualSituationAndNeverTheDateOrTheSeal(string situation, bool warns)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var setup = situation == "Manual" ? new HostSetup() : new HostSetup { Arguments = ["--Custody:SealSituation=External", "--Custody:SealDestination=https://zqx-vault.example.test/selos"] };
        await using var host = await StartedAsync(setup, cancellationToken);

        var status = await StatusAsync(host, cancellationToken);

        Assert.Equal(warns, status.GetProperty("sealWarning").ValueKind == JsonValueKind.String);
        Assert.DoesNotContain("lastSeal", status.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("CDG-SELO", status.ToString(), StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task TheStatusStopsWarningAsSoonAsTheSealIsConfirmed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await StartedAsync(new HostSetup(), cancellationToken);
        var before = await StatusAsync(host, cancellationToken);

        using var confirmed = await host.PostAsync(Confirm, Body(await IssueAsync(host, cancellationToken)), cancellationToken);
        var after = await StatusAsync(host, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        Assert.Equal((JsonValueKind.String, JsonValueKind.Null), (before.GetProperty("sealWarning").ValueKind, after.GetProperty("sealWarning").ValueKind));
    }

    private static async Task<JsonElement> StatusAsync(AuthenticatedHost host, CancellationToken cancellationToken)
    {
        using var response = await host.GetAsync("/status", cancellationToken);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return body.RootElement.Clone();
    }

    private static string Body(string seal) => JsonSerializer.Serialize(new { seal });

    private static async Task AssertRefusedAsync(HttpResponseMessage refused, CancellationToken cancellationToken)
    {
        using var body = JsonDocument.Parse(await refused.Content.ReadAsStringAsync(cancellationToken));
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal("seal", body.RootElement.GetProperty("field").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("reason").GetString()));
    }

    private static async Task<string> IssueAsync(AuthenticatedHost host, CancellationToken cancellationToken)
    {
        using var response = await host.GetAsync(Issue, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return body.RootElement.GetProperty("seal").GetString()!;
    }

    private static async Task<JsonElement> VerifyAsync(AuthenticatedHost host, string seal, CancellationToken cancellationToken)
    {
        using var response = await host.PostAsync(Verify, Body(seal), cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return body.RootElement.Clone();
    }

    private static async Task<JsonElement> ReadAsync(AuthenticatedHost host, CancellationToken cancellationToken)
    {
        using var response = await host.GetAsync(Verify, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return body.RootElement.Clone();
    }

    private static async Task<AuthenticatedHost> StartedAsync(HostSetup setup, CancellationToken cancellationToken)
    {
        var host = await AuthenticatedHost.StartAsync(setup with { Function = PersonFunctions.Administration }, cancellationToken);
        var until = DateTimeOffset.UtcNow + Patience;
        while ((await ReadAsync(host, cancellationToken)).GetProperty("last").ValueKind != JsonValueKind.Object)
        {
            Assert.True(DateTimeOffset.UtcNow < until, "The host never recorded its first verification.");
            await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);
        }

        return host;
    }
}
