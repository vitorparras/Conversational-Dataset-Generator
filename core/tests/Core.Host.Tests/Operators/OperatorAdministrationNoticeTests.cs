using Core.Host.Operators;
using Core.Persistence.Custody;
using Core.Persistence.Migrations;
using Core.Persistence.People;
using Core.Persistence.Tests.Fixtures;
using Core.Platform.Sessions;
using Npgsql;

namespace Core.Host.Tests.Operators;

/// <summary>
/// A password set again by someone else ends the sessions of the person and tells them how many (issue 595, CA-2, decision 0035); one set by the person themselves tells nothing,
/// and a reposition that is refused leaves the sessions open and tells nothing.
/// </summary>
[Trait("Category", "Database")]
public sealed class OperatorAdministrationNoticeTests
{
    [DatabaseFact]
    public async Task APasswordSetAgainEndsTheSessionsAndTellsThePersonHowManyItEnded()
    {
        await using var world = await World.OpenAsync();
        var (author, subject) = (await world.NewPersonAsync(), await world.NewPersonAsync());
        var kept = new[] { world.Sessions.Open(subject), world.Sessions.Open(subject) };

        var result = await world.Staff.SetPasswordAsync(author, subject, "a new secret", TestContext.Current.CancellationToken);

        Assert.Equal(PersonOutcome.Done, result.Outcome);
        Assert.All(kept, identifier => Assert.Null(world.Sessions.Resolve(identifier)));
        var notice = Assert.Single(await world.Staff.Notices.UnreadAsync(subject, TestContext.Current.CancellationToken));
        Assert.Equal((NoticeCode.PasswordReset, 2), (notice.Code, notice.SessionsEnded));
    }

    [DatabaseFact]
    public async Task APasswordSetAgainOfAPersonWithNoSessionIsToldWithZero()
    {
        await using var world = await World.OpenAsync();
        var (author, subject) = (await world.NewPersonAsync(), await world.NewPersonAsync());

        _ = await world.Staff.SetPasswordAsync(author, subject, "a new secret", TestContext.Current.CancellationToken);

        Assert.Equal(0, Assert.Single(await world.Staff.Notices.UnreadAsync(subject, TestContext.Current.CancellationToken)).SessionsEnded);
    }

    [DatabaseFact]
    public async Task APersonWhoSetsTheirOwnPasswordIsToldNothing()
    {
        await using var world = await World.OpenAsync();
        var person = await world.NewPersonAsync();

        _ = await world.Staff.SetPasswordAsync(person, person, "a new secret", TestContext.Current.CancellationToken);

        Assert.Empty(await world.Staff.Notices.UnreadAsync(person, TestContext.Current.CancellationToken));
    }

    [DatabaseFact]
    public async Task ARepositionThatIsRefusedLeavesTheSessionsOpenAndTellsNothing()
    {
        await using var world = await World.OpenAsync();
        var (author, subject) = (await world.NewPersonAsync(), await world.NewPersonAsync());
        var session = world.Sessions.Open(subject);

        var result = await world.Staff.SetPasswordAsync(author, subject, string.Empty, TestContext.Current.CancellationToken);

        Assert.NotEqual(PersonOutcome.Done, result.Outcome);
        Assert.Equal(subject, world.Sessions.Resolve(session));
        Assert.Empty(await world.Staff.Notices.UnreadAsync(subject, TestContext.Current.CancellationToken));
    }

    private sealed class World : IAsyncDisposable
    {
        private readonly ScratchDatabase database;
        private readonly NpgsqlDataSource source;

        private World(ScratchDatabase database, NpgsqlDataSource source)
        {
            this.database = database;
            this.source = source;
            Sessions = new SessionStore(new SessionLifetimes(TimeSpan.FromMinutes(30), TimeSpan.FromHours(8)), TimeProvider.System);
            Staff = new OperatorAdministration(new PersonRegistry(source), new PersonCredentials(source, new PasswordDerivation(1)), Sessions, new PersonNotices(source));
        }

        internal SessionStore Sessions { get; }

        internal OperatorAdministration Staff { get; }

        internal static async Task<World> OpenAsync()
        {
            var database = await ScratchDatabase.CreateAsync();
            _ = await database.MigrateAsync(EmbeddedMigrations.Load());
            return new World(database, NpgsqlDataSource.Create(database.AppConnectionString));
        }

        internal async Task<long> NewPersonAsync()
        {
            var made = await Staff.Registry.CreateAsync(
                new NewPerson($"zqx-pessoa-{Guid.NewGuid():N}", PersonFunctions.Administration), CustodyAuthor.System, TestContext.Current.CancellationToken);
            Assert.Equal(PersonOutcome.Done, made.Outcome);
            return made.PersonId!.Value;
        }

        public async ValueTask DisposeAsync()
        {
            await source.DisposeAsync();
            await database.DisposeAsync();
        }
    }
}
