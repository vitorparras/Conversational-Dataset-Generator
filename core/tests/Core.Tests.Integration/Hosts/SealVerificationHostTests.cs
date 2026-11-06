using System.Net;
using System.Text.Json;
using Core.Persistence.Custody.Verification;
using Core.Persistence.People;
using Core.Persistence.Tests.Fixtures;

namespace Core.Tests.Integration.Hosts;

/// <summary>
/// The verification against the seal at the built host over a real database (issue #62, part B1, CA-3 to CA-6): the route of a session reads the seal in its text and answers with the links
/// told apart from the seal and with what that verification protects in the situation declared; a base replaced and an end removed are told in the route and in the log, and a text that is
/// not a seal is refused with 400 before anything runs.
/// </summary>
[Trait("Category", "Database")]
public sealed class SealVerificationHostTests
{
    private const string Route = "/custody/verification";
    private const string LastEvent = "SELECT max(sequence) FROM public.custody_event";
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(60);

    [DatabaseFact]
    public async Task Ca3ABaseReplacedByAnotherThatIsCoherentIsToldAsADifferentDigestWhileTheChainCloses()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await StartedAsync(new HostSetup { Function = PersonFunctions.Administration, WithLogFile = true }, cancellationToken);
        var last = await host.NumberAsSuperuserAsync(LastEvent, cancellationToken);

        var shown = await VerifyAsync(host, ChainSeal.Of(DateTimeOffset.UtcNow, last, new byte[32]).ToText(), cancellationToken);

        Assert.Equal((true, false, "DigestDiffers"), Standing(shown));
        Assert.StartsWith("Resumo diferente", shown.GetProperty("seal").GetProperty("statement").GetString(), StringComparison.Ordinal);
        await LogHasAsync(host, "NÃO CONFERE (DigestDiffers)", cancellationToken);
    }

    [DatabaseFact]
    public async Task Ca4AnEndRemovedIsToldAsACountBelowTheSealedOneWhileTheChainCloses()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await StartedAsync(new HostSetup { Function = PersonFunctions.Administration, WithLogFile = true }, cancellationToken);
        var last = await host.NumberAsSuperuserAsync(LastEvent, cancellationToken);

        var shown = await VerifyAsync(host, ChainSeal.Of(DateTimeOffset.UtcNow, last + 500, new byte[32]).ToText(), cancellationToken);

        Assert.Equal((true, "CountBelowSealed"), (shown.GetProperty("passed").GetBoolean(), shown.GetProperty("seal").GetProperty("outcome").GetString()));
        Assert.StartsWith("Contagem menor que a selada", shown.GetProperty("seal").GetProperty("statement").GetString(), StringComparison.Ordinal);
        await LogHasAsync(host, "NÃO CONFERE (CountBelowSealed)", cancellationToken);
    }

    [DatabaseFact]
    public async Task ASealOfTheChainItselfHoldsAfterTheChainHasGrownAndNothingIsWrittenAsAFailure()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await StartedAsync(new HostSetup { Function = PersonFunctions.Administration, WithLogFile = true }, cancellationToken);
        var seal = await SealOfTheChainAsync(host, cancellationToken);

        var first = await VerifyAsync(host, seal, cancellationToken);
        var other = await host.EnterAsync(host.Name, host.Password, cancellationToken) ?? throw new InvalidOperationException("The second entry of the same person was refused.");
        var second = await VerifyAsync(host, seal, other, cancellationToken);

        Assert.All([first, second], shown => Assert.Equal((true, true, "Holds"), Standing(shown)));
        Assert.Equal(0L, await host.NumberAsSuperuserAsync("SELECT count(*) FROM public.custody_event WHERE reason = 'verification_failed'", cancellationToken));
        Assert.DoesNotContain("NÃO CONFERE", host.LogText, StringComparison.Ordinal);
    }

    [DatabaseTheory]
    [InlineData("")]
    [InlineData("zqx")]
    [InlineData("CDG-SELO-1|2031-03-04T05:06:07Z|3|abc|0000")]
    public async Task Ca6ATextThatIsNotASealIsRefusedWith400AndTheReasonAndNothingIsVerified(string text)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await StartedAsync(new HostSetup { Function = PersonFunctions.Administration }, cancellationToken);
        var before = await host.NumberAsSuperuserAsync("SELECT count(*) FROM public.custody_event", cancellationToken);

        using var refused = await host.PostAsync(Route, JsonSerializer.Serialize(new { seal = text }), cancellationToken);

        using var body = JsonDocument.Parse(await refused.Content.ReadAsStringAsync(cancellationToken));
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Equal("seal", body.RootElement.GetProperty("field").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("reason").GetString()));
        Assert.Equal(before, await host.NumberAsSuperuserAsync("SELECT count(*) FROM public.custody_event", cancellationToken));
    }

    [DatabaseFact]
    public async Task ABodyAboveTheLimitIsRefusedWith413AndNothingIsVerifiedWhileASealStillGoesThrough()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await StartedAsync(new HostSetup { Function = PersonFunctions.Administration }, cancellationToken);
        var before = await host.NumberAsSuperuserAsync("SELECT count(*) FROM public.custody_event", cancellationToken);

        using var refused = await host.PostAsync(Route, JsonSerializer.Serialize(new { seal = new string('a', 4096) }), cancellationToken);
        var after = await host.NumberAsSuperuserAsync("SELECT count(*) FROM public.custody_event", cancellationToken);
        var shown = await VerifyAsync(host, await SealOfTheChainAsync(host, cancellationToken), cancellationToken);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, refused.StatusCode);
        Assert.Equal(before, after);
        Assert.Equal((true, true, "Holds"), Standing(shown));
    }

    [DatabaseFact]
    public async Task AVerificationAskedForEntersTheHistoryWithWhoAskedAndTheOneTheHostRanByItselfHasNobody()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await StartedAsync(new HostSetup { Function = PersonFunctions.Administration }, cancellationToken);
        var asker = await host.NumberAsSuperuserAsync($"SELECT id FROM public.person WHERE name = '{host.Name}'", cancellationToken);

        _ = await VerifyAsync(host, await SealOfTheChainAsync(host, cancellationToken), cancellationToken);
        var history = (await ReadAsync(host, cancellationToken)).GetProperty("history").EnumerateArray().ToList();

        Assert.True(history.Count >= 2);
        Assert.Equal((asker, host.Name), (history[0].GetProperty("requestedBy").GetInt64(), history[0].GetProperty("requestedByName").GetString()));
        Assert.Equal(JsonValueKind.Null, history[^1].GetProperty("requestedBy").ValueKind);
    }

    [DatabaseTheory]
    [InlineData("External")]
    [InlineData("Internal")]
    [InlineData("Manual")]
    public async Task TheSituationDeclaredIsShownByTheConfigurationRouteWithTheArgumentAsItsOrigin(string situation)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await StartedAsync(Declaring(situation), cancellationToken);

        using var response = await host.GetAsync("/configuration", cancellationToken);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));

        var entry = body.RootElement.GetProperty("entries").EnumerateArray().Single(item => item.GetProperty("key").GetString() == "Custody:SealSituation");
        Assert.Equal((situation, "Argument"), (entry.GetProperty("value").GetString(), entry.GetProperty("origin").GetString()));
    }

    [DatabaseTheory]
    [InlineData("External", "base substituída")]
    [InlineData("Internal", "restauração equivocada")]
    [InlineData("Manual", "integridade")]
    public async Task Ca5EverySituationTellsWhatTheVerificationProtectsInTheAnswerAndInTheReadingOfTheLastOne(string situation, string words)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await StartedAsync(Declaring(situation), cancellationToken);
        var seal = await SealOfTheChainAsync(host, cancellationToken);

        var shown = await VerifyAsync(host, seal, cancellationToken);
        var read = await ReadAsync(host, cancellationToken);

        Assert.All([shown, read], answer => Assert.Equal(situation, answer.GetProperty("situation").GetString()));
        Assert.All([shown, read], answer => Assert.Contains(words, answer.GetProperty("protection").GetString(), StringComparison.Ordinal));
    }

    [DatabaseFact]
    public async Task Ca5TheManualSituationSaysNoSealWasRecordedAndThenTheDateOfTheLastOne()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await StartedAsync(new HostSetup { Function = PersonFunctions.Administration }, cancellationToken);
        var without = (await ReadAsync(host, cancellationToken)).GetProperty("protection").GetString();

        await host.RunAsSuperuserAsync(
            "INSERT INTO public.custody_event (actor_system, item_kind, item_id, step, reason) "
            + "SELECT true, 'custody_chain', max(sequence), 'seal_recorded', 'administrative_act' FROM public.custody_event",
            cancellationToken);
        var moment = await host.TextAsSuperuserAsync(
            "SELECT to_char(occurred_at AT TIME ZONE 'UTC', 'YYYY-MM-DD\"T\"HH24:MI:SS\"Z\"') FROM public.custody_event WHERE step = 'seal_recorded'", cancellationToken);
        var with = (await ReadAsync(host, cancellationToken)).GetProperty("protection").GetString();

        Assert.Contains("Nenhum selo foi registrado ainda", without, StringComparison.Ordinal);
        Assert.Contains(moment, with, StringComparison.Ordinal);
    }

    [DatabaseTheory]
    [InlineData("External")]
    [InlineData("Internal")]
    public async Task TheReadingOfTheLastVerificationSaysNoLastSealWithoutOneAndThenTheMomentOfTheRecordInTheSituationsThatSendTheSeal(string situation)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await StartedAsync(Declaring(situation), cancellationToken);
        var without = await ReadAsync(host, cancellationToken);

        await host.RunAsSuperuserAsync(
            "INSERT INTO public.custody_event (actor_system, item_kind, item_id, step, reason) "
            + "SELECT true, 'custody_chain', max(sequence), 'seal_recorded', 'administrative_act' FROM public.custody_event",
            cancellationToken);
        var moment = await host.TextAsSuperuserAsync(
            "SELECT to_char(occurred_at AT TIME ZONE 'UTC', 'YYYY-MM-DD\"T\"HH24:MI:SS') FROM public.custody_event WHERE step = 'seal_recorded'", cancellationToken);
        var with = await ReadAsync(host, cancellationToken);

        Assert.Equal(JsonValueKind.Null, without.GetProperty("lastSeal").ValueKind);
        Assert.StartsWith(moment, with.GetProperty("lastSeal").GetString(), StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task Ca5ASealThatDoesNotMatchIsNeverToldInTheProtectionAsAChainThatMatchesIt()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await StartedAsync(new HostSetup { Function = PersonFunctions.Administration }, cancellationToken);
        await host.RunAsSuperuserAsync(
            "INSERT INTO public.custody_event (actor_system, item_kind, item_id, step, reason) "
            + "SELECT true, 'custody_chain', max(sequence), 'seal_recorded', 'administrative_act' FROM public.custody_event",
            cancellationToken);
        var last = await host.NumberAsSuperuserAsync(LastEvent, cancellationToken);

        var shown = await VerifyAsync(host, ChainSeal.Of(DateTimeOffset.UtcNow, last, new byte[32]).ToText(), cancellationToken);

        var protection = shown.GetProperty("protection").GetString();
        Assert.False(shown.GetProperty("seal").GetProperty("holds").GetBoolean());
        Assert.Contains("A comparação com o selo de", protection, StringComparison.Ordinal);
        Assert.DoesNotContain("confere com", protection, StringComparison.Ordinal);
    }

    [DatabaseTheory]
    [InlineData("External")]
    [InlineData("Internal")]
    public async Task TheDestinationDeclaredIsNeverInTheAnswers(string situation)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await StartedAsync(Declaring(situation), cancellationToken);
        var seal = await SealOfTheChainAsync(host, cancellationToken);

        var shown = await VerifyAsync(host, seal, cancellationToken);
        var read = await ReadAsync(host, cancellationToken);

        Assert.DoesNotContain("zqx-vault", shown.ToString() + read, StringComparison.Ordinal);
    }

    private static (bool Passed, bool Holds, string? Outcome) Standing(JsonElement shown) =>
        (shown.GetProperty("passed").GetBoolean(), shown.GetProperty("seal").GetProperty("holds").GetBoolean(), shown.GetProperty("seal").GetProperty("outcome").GetString());

    private static HostSetup Declaring(string situation) =>
        situation == "Manual"
            ? new HostSetup { Function = PersonFunctions.Administration }
            : new HostSetup { Function = PersonFunctions.Administration, Arguments = [$"--Custody:SealSituation={situation}", "--Custody:SealDestination=https://zqx-vault.example.test/selos"] };

    private static async Task<AuthenticatedHost> StartedAsync(HostSetup setup, CancellationToken cancellationToken)
    {
        var host = await AuthenticatedHost.StartAsync(setup, cancellationToken);
        var until = DateTimeOffset.UtcNow + Patience;
        while ((await ReadAsync(host, cancellationToken)).GetProperty("last").ValueKind != JsonValueKind.Object)
        {
            Assert.True(DateTimeOffset.UtcNow < until, "The host never recorded its first verification.");
            await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);
        }

        return host;
    }

    private static async Task<string> SealOfTheChainAsync(AuthenticatedHost host, CancellationToken cancellationToken)
    {
        var last = await host.NumberAsSuperuserAsync(LastEvent, cancellationToken);
        var digest = await host.TextAsSuperuserAsync($"SELECT encode(digest, 'hex') FROM public.custody_event WHERE sequence = {last}", cancellationToken);
        return ChainSeal.Of(DateTimeOffset.UtcNow, last, Convert.FromHexString(digest)).ToText();
    }

    private static Task<JsonElement> VerifyAsync(AuthenticatedHost host, string seal, CancellationToken cancellationToken) => VerifyAsync(host, seal, host.Identifier, cancellationToken);

    // The limit counts per session: a second verification within the window has to come from another session.
    private static async Task<JsonElement> VerifyAsync(AuthenticatedHost host, string seal, string session, CancellationToken cancellationToken)
    {
        using var response = await host.SendAsAsync(HttpMethod.Post, Route, JsonSerializer.Serialize(new { seal }), session, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return body.RootElement.Clone();
    }

    private static async Task<JsonElement> ReadAsync(AuthenticatedHost host, CancellationToken cancellationToken)
    {
        using var response = await host.GetAsync(Route, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return body.RootElement.Clone();
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
