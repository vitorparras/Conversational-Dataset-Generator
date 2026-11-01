using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.People;

/// <summary>
/// The salt and the result of a password are the application's and no other role's (#471): a role that connects and holds no grant is refused every reading and writing of the table,
/// and PUBLIC holds nothing on it, on the table nor on a column. The application's own use of the table is proved by the tests of the credentials.
/// </summary>
[Trait("Category", "Database")]
public sealed class PersonCredentialPrivateTests : IClassFixture<PeopleDatabase>
{
    private const string Insert =
        "INSERT INTO public.person_credential (person_id, algorithm, work_factor, salt, derived) "
        + "VALUES (1, 'zqx', 1, decode('00000000000000000000000000000000', 'hex'), decode('00', 'hex'))";
    private const string TableGrantees =
        "SELECT DISTINCT grantee FROM information_schema.role_table_grants WHERE table_schema = 'public' AND table_name = 'person_credential'";
    private const string ColumnGrantees =
        "SELECT DISTINCT grantee FROM information_schema.role_column_grants WHERE table_schema = 'public' AND table_name = 'person_credential'";

    private static readonly string[] Statements =
    [
        "SELECT salt, derived FROM public.person_credential",
        "SELECT count(*) FROM public.person_credential",
        Insert,
        "UPDATE public.person_credential SET derived = decode('00', 'hex')",
    ];

    private readonly PeopleDatabase database;

    public PersonCredentialPrivateTests(PeopleDatabase database)
    {
        this.database = database;
    }

    [DatabaseFact]
    public async Task ARoleThatConnectsAndHoldsNoGrantIsRefusedTheSaltAndTheResultAndEveryWriting()
    {
        await using var role = await database.BareRoleAsync();

        var states = new List<string?>();
        foreach (var statement in Statements)
        {
            states.Add((await PeopleDatabase.RefusedOverAsync(role.ConnectionString, statement)).SqlState);
        }

        Assert.All(states, state => Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, state));
    }

    [DatabaseFact]
    public async Task PublicHoldsNothingOnTheTableNorOnAnyColumnAndTheApplicationIsStillGranted()
    {
        var onTable = await database.OwnerColumnAsync(TableGrantees);
        var onColumns = await database.OwnerColumnAsync(ColumnGrantees);

        Assert.DoesNotContain("PUBLIC", onTable.Concat(onColumns), StringComparer.Ordinal);
        Assert.Contains("app", onTable);
        Assert.Contains("app", onColumns);
    }
}
