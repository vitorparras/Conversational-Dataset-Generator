using System.Net;
using System.Text.Json;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Tests.Integration.Hosts;

/// <summary>
/// The entry of a right password that meets another writer at the chain (issue #677): it is recorded again, and if the chain stays busy it is answered 503 with the reason, never 401, which says the
/// credential is wrong; the entry that was not recorded counts nothing against the person, so the next one is not made to wait.
/// </summary>
[Trait("Category", "Database")]
public sealed class SignInChainBusyHostTests
{
    private const string Tries = "public.zqx_tries";

    [DatabaseFact]
    public async Task RightPasswordThatMeetsAnotherWriterFourTimesIsRecordedAgainAndOpensTheSession()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await AuthenticatedHost.StartAsync(new HostSetup(), cancellationToken);
        await ConflictOnEntryAsync(host, conflicts: 4, cancellationToken);

        using var response = await EnterAsync(host, cancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(5, await host.NumberAsSuperuserAsync($"SELECT last_value FROM {Tries}", cancellationToken));
    }

    [DatabaseFact]
    public async Task RightPasswordThatAlwaysMeetsAnotherWriterIsAnswered503WithTheReasonAndCountsNothingAgainstThePerson()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await AuthenticatedHost.StartAsync(new HostSetup(), cancellationToken);
        await ConflictOnEntryAsync(host, conflicts: int.MaxValue, cancellationToken);
        var attemptsBefore = await host.NumberAsSuperuserAsync("SELECT count(*) FROM public.sign_in_attempt", cancellationToken);

        using var busy = await EnterAsync(host, cancellationToken);
        using var body = JsonDocument.Parse(await busy.Content.ReadAsStringAsync(cancellationToken));
        await host.RunAsSuperuserAsync("DROP TRIGGER zqx_conflict ON public.sign_in_attempt", cancellationToken);
        var started = DateTimeOffset.UtcNow;
        using var next = await EnterAsync(host, cancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, busy.StatusCode);
        Assert.Contains("nada foi recusado", body.RootElement.GetProperty("reason").GetString(), StringComparison.Ordinal);
        Assert.False(busy.Headers.Contains("Set-Cookie"));
        Assert.Equal(attemptsBefore, await host.NumberAsSuperuserAsync("SELECT count(*) FROM public.sign_in_attempt", cancellationToken) - 1);
        Assert.Equal(HttpStatusCode.NoContent, next.StatusCode);
        Assert.True(DateTimeOffset.UtcNow - started < TimeSpan.FromSeconds(1), "The entry after the busy one waited a delay that only a refusal earns.");
    }

    [DatabaseFact]
    public async Task WrongPasswordIsStillAnswered401EvenWhileTheChainIsBusy()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await AuthenticatedHost.StartAsync(new HostSetup(), cancellationToken);
        await ConflictOnEntryAsync(host, conflicts: int.MaxValue, cancellationToken);

        using var response = await host.SendAsAsync(HttpMethod.Post, "/session", $$"""{"name":"{{host.Name}}","password":"zqx-wrong"}""", session: null, cancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static Task<HttpResponseMessage> EnterAsync(AuthenticatedHost host, CancellationToken cancellationToken) =>
        host.SendAsAsync(HttpMethod.Post, "/session", $$"""{"name":"{{host.Name}}","password":"{{host.Password}}"}""", session: null, cancellationToken);

    /// <summary>The first inserts of an attempt of the entry are refused with a serialization failure, as a writer that meets another at the chain would be, and the tries are counted.</summary>
    private static async Task ConflictOnEntryAsync(AuthenticatedHost host, int conflicts, CancellationToken cancellationToken)
    {
        var application = new NpgsqlConnectionStringBuilder(host.ApplicationConnectionString).Username;
        await host.RunAsSuperuserAsync(
            $"CREATE SEQUENCE {Tries}; GRANT USAGE ON SEQUENCE {Tries} TO \"{application}\"; "
            + $"CREATE FUNCTION public.zqx_conflict() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF nextval('{Tries}') <= {conflicts} "
            + "THEN RAISE EXCEPTION 'zqx' USING ERRCODE = '40001'; END IF; RETURN NEW; END $$; "
            + "CREATE TRIGGER zqx_conflict BEFORE INSERT ON public.sign_in_attempt FOR EACH ROW EXECUTE FUNCTION public.zqx_conflict()",
            cancellationToken);
    }
}
