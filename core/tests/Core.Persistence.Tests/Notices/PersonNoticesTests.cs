using Core.Persistence.People;
using Core.Persistence.Tests.Fixtures;
using Core.Persistence.Tests.People;
using Npgsql;

namespace Core.Persistence.Tests.Notices;

/// <summary>
/// The notices of a person (issue 595, CA-2 and CA-4): a password set again is told with how many sessions it ended, the list shows only the unread of that person, newest first, and reading
/// one takes it from the list and keeps the row.
/// </summary>
[Trait("Category", "Database")]
public sealed class PersonNoticesTests : IClassFixture<PeopleDatabase>
{
    private readonly PeopleDatabase database;

    public PersonNoticesTests(PeopleDatabase database)
    {
        this.database = database;
    }

    [DatabaseFact]
    public async Task APasswordSetAgainIsToldWithHowManySessionsItEnded()
    {
        var person = await database.NewPersonAsync();

        await (await database.NoticesAsync()).RecordPasswordResetAsync(person, 3, TestContext.Current.CancellationToken);

        var unread = Assert.Single(await (await database.NoticesAsync()).UnreadAsync(person, TestContext.Current.CancellationToken));
        Assert.Equal((NoticeCode.PasswordReset, 3), (unread.Code, unread.SessionsEnded));
    }

    [DatabaseFact]
    public async Task APasswordSetAgainThatEndedNoSessionIsToldWithZero()
    {
        var person = await database.NewPersonAsync();

        await (await database.NoticesAsync()).RecordPasswordResetAsync(person, 0, TestContext.Current.CancellationToken);

        Assert.Equal(0, Assert.Single(await (await database.NoticesAsync()).UnreadAsync(person, TestContext.Current.CancellationToken)).SessionsEnded);
    }

    [DatabaseFact]
    public async Task ANegativeNumberOfSessionsIsNotRecorded()
    {
        var person = await database.NewPersonAsync();
        var notices = await database.NoticesAsync();

        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => notices.RecordPasswordResetAsync(person, -1, TestContext.Current.CancellationToken));

        Assert.Empty(await notices.UnreadAsync(person, TestContext.Current.CancellationToken));
    }

    [DatabaseFact]
    public async Task TheListShowsTheNewestFirstAndOnlyThoseOfThePerson()
    {
        var (person, other) = (await database.NewPersonAsync(), await database.NewPersonAsync());
        var notices = await database.NoticesAsync();
        await notices.RecordPasswordResetAsync(person, 1, TestContext.Current.CancellationToken);
        await notices.RecordPasswordResetAsync(other, 9, TestContext.Current.CancellationToken);
        await notices.RecordPasswordResetAsync(person, 2, TestContext.Current.CancellationToken);

        var listed = await notices.UnreadAsync(person, TestContext.Current.CancellationToken);

        Assert.Equal([2, 1], listed.Select(notice => notice.SessionsEnded));
    }

    [DatabaseFact]
    public async Task ANoticeReadLeavesTheListOfThePersonAndStaysRecorded()
    {
        var person = await database.NewPersonAsync();
        var notices = await database.NoticesAsync();
        await notices.RecordPasswordResetAsync(person, 4, TestContext.Current.CancellationToken);
        var id = (await notices.UnreadAsync(person, TestContext.Current.CancellationToken))[0].Id;

        var marked = await notices.MarkReadAsync(person, id, TestContext.Current.CancellationToken);

        Assert.True(marked);
        Assert.Empty(await notices.UnreadAsync(person, TestContext.Current.CancellationToken));
        Assert.Equal("4|read", await database.TextAsync($"SELECT sessions_ended || '|' || CASE WHEN read_at >= created_at THEN 'read' ELSE 'wrong' END FROM public.person_notice WHERE id = {id}"));
    }

    [DatabaseFact]
    public async Task ANoticeAlreadyReadIsNotMarkedAgain()
    {
        var person = await database.NewPersonAsync();
        var notices = await database.NoticesAsync();
        await notices.RecordPasswordResetAsync(person, 1, TestContext.Current.CancellationToken);
        var id = (await notices.UnreadAsync(person, TestContext.Current.CancellationToken))[0].Id;
        _ = await notices.MarkReadAsync(person, id, TestContext.Current.CancellationToken);

        Assert.False(await notices.MarkReadAsync(person, id, TestContext.Current.CancellationToken));
    }

    [DatabaseFact]
    public async Task TheNoticeOfAnotherPersonIsNotMarkedAndStaysInTheirList()
    {
        var (owner, other) = (await database.NewPersonAsync(), await database.NewPersonAsync());
        var notices = await database.NoticesAsync();
        await notices.RecordPasswordResetAsync(owner, 1, TestContext.Current.CancellationToken);
        var id = (await notices.UnreadAsync(owner, TestContext.Current.CancellationToken))[0].Id;

        var marked = await notices.MarkReadAsync(other, id, TestContext.Current.CancellationToken);

        Assert.False(marked);
        _ = Assert.Single(await notices.UnreadAsync(owner, TestContext.Current.CancellationToken));
    }

    [DatabaseFact]
    public async Task APersonNobodyRegisteredIsNotTold()
    {
        var notices = await database.NoticesAsync();

        var refused = await Assert.ThrowsAsync<PostgresException>(() => notices.RecordPasswordResetAsync(long.MaxValue, 1, TestContext.Current.CancellationToken));

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, refused.SqlState);
    }
}
