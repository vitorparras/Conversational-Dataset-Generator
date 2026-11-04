using System.Net;
using Core.Persistence.Tests.Fixtures;

namespace Core.Tests.Integration.Sessions;

/// <summary>
/// The record of the entry through the built host (#71, part 2, CA-2, CA-4): every attempt that reaches the check leaves a row and an event of the chain, authored by the person who signed in
/// or whose password was wrong and by the system for a name nobody goes by, under the prefix of the origin; and neither the name typed nor the password is in the database or in the log.
/// </summary>
[Trait("Category", "Database")]
public sealed class SessionEntryRecordTests
{
    private const string Records = "SELECT coalesce(string_agg(concat_ws('|', e.step, e.reason, coalesce(p.name, 'system'), coalesce(a.network::text, '-')), ';' ORDER BY e.sequence), '') "
        + "FROM public.custody_event e JOIN public.sign_in_attempt a ON a.id = e.item_id LEFT JOIN public.person p ON p.id = e.actor_person_id WHERE e.item_kind = 'sign_in_attempt'";

    [DatabaseFact]
    public async Task EveryAttemptThatReachesTheCheckIsRecordedWithThePersonOrNobodyAndThePrefix()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scenario = await SessionScenario.StartAsync("00:30:00", cancellationToken);

        using var accepted = await scenario.EnterAsync(scenario.Name, scenario.Password, cancellationToken);
        using var wrong = await scenario.EnterAsync(scenario.Name, "zqx-senha-errada", cancellationToken);
        using var nobody = await scenario.EnterAsync($"zqx-fantasma-{Guid.NewGuid():N}", scenario.Password, cancellationToken);
        using var noPassword = await scenario.EnterAsync(scenario.NameWithoutPassword, scenario.Password, cancellationToken);
        using var deactivated = await scenario.EnterAsync(scenario.NameDeactivated, scenario.Password, cancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);
        HttpResponseMessage[] refusals = [wrong, nobody, noPassword, deactivated];
        Assert.All(refusals, refused => Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode));
        Assert.Equal(
            string.Join(
                ';',
                $"sign_in_accepted|credentials_accepted|{scenario.Name}|127.0.0.0/24",
                $"sign_in_refused|password_wrong|{scenario.Name}|127.0.0.0/24",
                "sign_in_refused|account_not_recognized|system|127.0.0.0/24",
                "sign_in_refused|account_not_recognized|system|127.0.0.0/24",
                "sign_in_refused|account_not_recognized|system|127.0.0.0/24"),
            await scenario.HeldAsync(Records, cancellationToken));
    }

    [DatabaseFact]
    public async Task ARightPasswordWhoseAcceptanceTheChainDoesNotTakeIsRefusedAndNoEventSaysTheAccountIsNotRecognized()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scenario = await SessionScenario.StartAsync("00:30:00", cancellationToken);
        await scenario.RunAsOwnerAsync(
            "CREATE FUNCTION public.zqx_conflict() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'zqx' USING ERRCODE = '40001'; END $$",
            "CREATE TRIGGER zqx_conflict BEFORE INSERT ON public.custody_event FOR EACH ROW WHEN (NEW.step = 'sign_in_accepted') EXECUTE FUNCTION public.zqx_conflict()");

        using var refused = await scenario.EnterAsync(scenario.Name, scenario.Password, cancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);
        Assert.Empty(refused.Headers.Where(header => header.Key == "Set-Cookie"));
        Assert.Equal("0", await scenario.HeldAsync("SELECT count(*) FROM public.custody_event WHERE step LIKE 'sign_in%'", cancellationToken));
        Assert.Equal("0", await scenario.HeldAsync("SELECT count(*) FROM public.sign_in_attempt", cancellationToken));
    }

    [DatabaseFact]
    public async Task NeitherTheNameTypedNorThePasswordIsInTheRowsTheEventsOrTheLog()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scenario = await SessionScenario.StartAsync("00:30:00", cancellationToken);
        var typedName = $"zqx-fantasma-{Guid.NewGuid():N}";
        var typedPassword = $"zqx-tentada-{Guid.NewGuid():N}";

        using var nobody = await scenario.EnterAsync(typedName, typedPassword, cancellationToken);
        using var wrong = await scenario.EnterAsync(scenario.Name, typedPassword, cancellationToken);
        using var accepted = await scenario.EnterAsync(scenario.Name, scenario.Password, cancellationToken);

        var held = await scenario.HeldAsync(
            "SELECT coalesce((SELECT string_agg(a::text, ' ') FROM public.sign_in_attempt a), '') || coalesce((SELECT string_agg(e::text, ' ') FROM public.custody_event e), '')",
            cancellationToken);
        var written = await scenario.StopAndReadEverythingAsync();
        string[] secrets = [typedName, typedPassword, scenario.Password];
        Assert.All(secrets, secret =>
        {
            Assert.DoesNotContain(secret, held, StringComparison.Ordinal);
            Assert.DoesNotContain(secret, written, StringComparison.Ordinal);
        });
        Assert.DoesNotContain("127.0.0.1", held, StringComparison.Ordinal);
    }
}
