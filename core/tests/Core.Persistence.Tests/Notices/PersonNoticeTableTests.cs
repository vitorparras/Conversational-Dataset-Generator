using System.Globalization;
using Core.Persistence.Tests.Fixtures;
using Core.Persistence.Tests.People;
using Npgsql;

namespace Core.Persistence.Tests.Notices;

/// <summary>
/// The migration 0038 (issue 595, CA-4): the table takes a code from the closed list with the number only where it belongs,
/// and nobody deletes, empties or rewrites a notice but to read it once.
/// </summary>
[Trait("Category", "Database")]
public sealed class PersonNoticeTableTests : IClassFixture<PeopleDatabase>
{
    private readonly PeopleDatabase database;

    public PersonNoticeTableTests(PeopleDatabase database)
    {
        this.database = database;
    }

    [DatabaseTheory]
    [InlineData("'other_code', NULL")]
    [InlineData("'password_reset', NULL")]
    [InlineData("'password_reset', -1")]
    [InlineData("'password_reset', 1000000")]
    [InlineData("'domain_read_only', 2")]
    public async Task ACodeOutsideTheListOrANumberWhereItDoesNotBelongIsRefused(string values)
    {
        var person = await database.NewPersonAsync();

        var refused = await database.RefusedToOwnerAsync($"INSERT INTO public.person_notice (person_id, code, sessions_ended) VALUES ({person}, {values})");

        Assert.Equal(PostgresErrorCodes.CheckViolation, refused.SqlState);
    }

    [DatabaseTheory]
    [InlineData("UPDATE public.person_notice SET code = 'domain_read_only', sessions_ended = NULL WHERE id = {0}")]
    [InlineData("UPDATE public.person_notice SET created_at = created_at - interval '1 day' WHERE id = {0}")]
    [InlineData("UPDATE public.person_notice SET person_id = person_id + 1 WHERE id = {0}")]
    [InlineData("UPDATE public.person_notice SET read_at = NULL WHERE id = {0}")]
    public async Task ANoticeIsNeverRewrittenEvenByTheOwner(string statement)
    {
        var id = await UnreadNoticeIdAsync();

        var refused = await database.RefusedToOwnerAsync(string.Format(CultureInfo.InvariantCulture, statement, id));

        Assert.Equal("CE001", refused.SqlState);
    }

    [DatabaseTheory]
    [InlineData("DELETE FROM public.person_notice")]
    [InlineData("TRUNCATE public.person_notice")]
    public async Task NoticesAreNeverDeletedNorEmptiedEvenByTheOwner(string statement)
    {
        var refused = await database.RefusedToOwnerAsync(statement);

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, refused.SqlState);
    }

    [DatabaseFact]
    public async Task TheApplicationCannotChooseWhenANoticeWasMade()
    {
        var refused = await database.RefusedToAppAsync("UPDATE public.person_notice SET created_at = now()");

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, refused.SqlState);
    }

    private async Task<long> UnreadNoticeIdAsync()
    {
        var person = await database.NewPersonAsync();
        var notices = await database.NoticesAsync();
        await notices.RecordPasswordResetAsync(person, 1, TestContext.Current.CancellationToken);
        return (await notices.UnreadAsync(person, TestContext.Current.CancellationToken))[0].Id;
    }
}
