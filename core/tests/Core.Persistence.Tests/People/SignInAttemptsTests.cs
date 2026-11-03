using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using Core.Persistence.Custody;
using Core.Persistence.People;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.People;

/// <summary>
/// The record of the entry (#71, part 2, CA-2, CA-4 and CA-5): every attempt is a row with the network prefix and an event of the chain, written together or not at all; the person is the
/// author of what is accepted and of a wrong password and nobody is the author of a name nobody goes by; the whole address is never kept; and the series groups the refusals by prefix.
/// </summary>
[Trait("Category", "Database")]
public sealed class SignInAttemptsTests : IClassFixture<PeopleDatabase>
{
    private const string Row = "SELECT concat_ws('|', e.step, e.reason, coalesce(e.actor_person_id::text, 'system'), coalesce(a.network::text, '-')) "
        + "FROM public.custody_event e JOIN public.sign_in_attempt a ON a.id = e.item_id WHERE e.item_kind = 'sign_in_attempt' AND e.sequence = ";

    private readonly PeopleDatabase database;

    public SignInAttemptsTests(PeopleDatabase database)
    {
        this.database = database;
    }

    [Theory]
    [InlineData("203.0.113.77", "203.0.113.0/24")]
    [InlineData("::ffff:198.51.100.200", "198.51.100.0/24")]
    [InlineData("2001:db8:abcd:1234:5678::1", "2001:db8:abcd::/48")]
    [InlineData("fe80::1", "fe80::/48")]
    public void TheOriginIsKeptAsThePrefixAndNeverAsTheWholeAddress(string origin, string expected) =>
        Assert.Equal(expected, SignInAttempts.NetworkOf(IPAddress.Parse(origin)));

    [Fact]
    public void AnUnknownOriginHasNoPrefix() => Assert.Null(SignInAttempts.NetworkOf(null));

    [DatabaseFact]
    public async Task AnAcceptedAttemptIsARowAndAnEventAuthoredByThePersonWhoSignedIn()
    {
        var person = await NewPersonAsync();

        var recorded = await (await AttemptsAsync()).AcceptedAsync(person, IPAddress.Parse("203.0.113.9"), TestContext.Current.CancellationToken);

        Assert.Equal($"sign_in_accepted|credentials_accepted|{person}|203.0.113.0/24", await TextAsync(Row + recorded.Sequence));
    }

    [DatabaseFact]
    public async Task ARefusalForAWrongPasswordIsAuthoredByThePerson()
    {
        var person = await NewPersonAsync();

        var recorded = await (await AttemptsAsync()).RefusedAsync(person, SignInRefusal.PasswordWrong, IPAddress.Parse("2001:db8:1:2::3"), TestContext.Current.CancellationToken);

        Assert.Equal($"sign_in_refused|password_wrong|{person}|2001:db8:1::/48", await TextAsync(Row + recorded.Sequence));
    }

    [DatabaseFact]
    public async Task ARefusalForAnAccountNobodyGoesByIsAuthoredByTheSystemAndAnUnknownOriginIsKeptAsNone()
    {
        var recorded = await (await AttemptsAsync()).RefusedAsync(null, SignInRefusal.AccountNotRecognized, null, TestContext.Current.CancellationToken);

        Assert.Equal("sign_in_refused|account_not_recognized|system|-", await TextAsync(Row + recorded.Sequence));
    }

    [DatabaseFact]
    public async Task AWrongPasswordWithNoPersonIsRefusedAndLeavesNeitherARowNorAnEvent()
    {
        var rows = await TextAsync("SELECT (SELECT count(*) FROM public.sign_in_attempt) || '|' || (SELECT count(*) FROM public.custody_event)");

        var attempts = await AttemptsAsync();

        _ = await Assert.ThrowsAsync<ArgumentException>(
            () => attempts.RefusedAsync(null, SignInRefusal.PasswordWrong, IPAddress.Loopback, TestContext.Current.CancellationToken));

        Assert.Equal(rows, await TextAsync("SELECT (SELECT count(*) FROM public.sign_in_attempt) || '|' || (SELECT count(*) FROM public.custody_event)"));
    }

    [DatabaseFact]
    public async Task AnAttemptOfADeactivatedPersonIsNotRecordedAsTheirOwnAndLeavesNoRowBehind()
    {
        var registry = await database.RegistryAsync();
        var person = PeopleDatabase.Id(await registry.CreateAsync(new NewPerson(PeopleDatabase.NewName(), PersonFunctions.Accepted[0]), CustodyAuthor.System, TestContext.Current.CancellationToken));
        _ = await registry.DeactivateAsync(person, CustodyAuthor.System, TestContext.Current.CancellationToken);
        var rows = await TextAsync("SELECT count(*) FROM public.sign_in_attempt");

        var recorded = await (await AttemptsAsync()).AcceptedAsync(person, IPAddress.Loopback, TestContext.Current.CancellationToken);

        Assert.Equal((CustodyOutcome.ActorDeactivated, rows), (recorded.Outcome, await TextAsync("SELECT count(*) FROM public.sign_in_attempt")));
    }

    [DatabaseFact]
    public async Task TheSeriesGroupsTheRefusalsByPrefixFromTenOnAndLeavesOutWhatWasAccepted()
    {
        var person = await NewPersonAsync();
        var token = TestContext.Current.CancellationToken;
        for (var index = 0; index < 11; index++)
        {
            _ = await (await AttemptsAsync()).RefusedAsync(null, SignInRefusal.AccountNotRecognized, IPAddress.Parse($"203.0.113.{index}"), token);
        }

        for (var index = 0; index < SignInAttempts.LeastRefusals; index++)
        {
            _ = await (await AttemptsAsync()).RefusedAsync(person, SignInRefusal.PasswordWrong, IPAddress.Parse($"198.51.100.{index}"), token);
        }

        for (var index = 0; index < SignInAttempts.LeastRefusals - 1; index++)
        {
            _ = await (await AttemptsAsync()).RefusedAsync(null, SignInRefusal.AccountNotRecognized, IPAddress.Parse($"192.0.2.{index}"), token);
        }

        for (var index = 0; index < SignInAttempts.LeastRefusals; index++)
        {
            _ = await (await AttemptsAsync()).AcceptedAsync(person, IPAddress.Parse($"100.64.0.{index}"), token);
        }

        var series = await (await AttemptsAsync()).SeriesAsync(DateTimeOffset.UtcNow.AddMinutes(30), token);

        (string, int)[] expected = [("203.0.113.0/24", 11), ("198.51.100.0/24", SignInAttempts.LeastRefusals)];
        Assert.Equal(expected, series.Select(each => (each.Network, each.Refusals)));
        Assert.All(series, each => Assert.True(each.First <= each.Last));
    }

    [DatabaseFact]
    public async Task TheSeriesLooksOnlyAtTheHourThatEndsWhereAsked()
    {
        var token = TestContext.Current.CancellationToken;
        for (var index = 0; index < SignInAttempts.LeastRefusals; index++)
        {
            _ = await (await AttemptsAsync()).RefusedAsync(null, SignInRefusal.AccountNotRecognized, IPAddress.Parse($"233.252.0.{index}"), token);
        }

        var inside = await (await AttemptsAsync()).SeriesAsync(DateTimeOffset.UtcNow.AddMinutes(30), token);
        var past = await (await AttemptsAsync()).SeriesAsync(DateTimeOffset.UtcNow.AddMinutes(70), token);
        var before = await (await AttemptsAsync()).SeriesAsync(DateTimeOffset.UtcNow.AddMinutes(-70), token);

        Assert.Contains(inside, each => each.Network == "233.252.0.0/24");
        Assert.DoesNotContain(past, each => each.Network == "233.252.0.0/24");
        Assert.DoesNotContain(before, each => each.Network == "233.252.0.0/24");
    }

    [DatabaseFact]
    public async Task TheSeriesAddsNoEventToTheChain()
    {
        var events = await TextAsync("SELECT count(*) FROM public.custody_event");

        _ = await (await AttemptsAsync()).SeriesAsync(DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);

        Assert.Equal(events, await TextAsync("SELECT count(*) FROM public.custody_event"));
    }

    [DatabaseFact]
    public async Task TheWholeAddressIsNotInTheRowsNorInTheEvents()
    {
        var token = TestContext.Current.CancellationToken;
        var person = await NewPersonAsync();

        _ = await (await AttemptsAsync()).RefusedAsync(null, SignInRefusal.AccountNotRecognized, IPAddress.Parse("198.18.5.77"), token);
        _ = await (await AttemptsAsync()).AcceptedAsync(person, IPAddress.Parse("198.18.5.78"), token);

        var held = await TextAsync("SELECT coalesce(string_agg(a::text, ' '), '') || coalesce(string_agg(e::text, ' '), '') FROM public.sign_in_attempt a, public.custody_event e");
        Assert.DoesNotContain("198.18.5.77", held, StringComparison.Ordinal);
        Assert.DoesNotContain("198.18.5.78", held, StringComparison.Ordinal);
    }

    private async Task<SignInAttempts> AttemptsAsync() => new(await database.ApplicationAsync());

    private async Task<long> NewPersonAsync()
    {
        var registry = await database.RegistryAsync();
        return PeopleDatabase.Id(await registry.CreateAsync(new NewPerson(PeopleDatabase.NewName(), PersonFunctions.Accepted[0]), CustodyAuthor.System, TestContext.Current.CancellationToken));
    }

    [SuppressMessage("Security", "CA2100", Justification = "Each statement is a literal of this class, never built from input.")]
    private async Task<string> TextAsync(string statement)
    {
        await using var connection = await Sql.OpenAsync((await database.ScratchAsync()).OwnerConnectionString);
        await using var command = new NpgsqlCommand(statement, connection);
        return Convert.ToString(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken), CultureInfo.InvariantCulture)!;
    }
}
