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
    private const string TableGrantees = """
        SELECT DISTINCT CASE WHEN acl.grantee = 0 THEN 'PUBLIC' ELSE pg_catalog.pg_get_userbyid(acl.grantee) END
        FROM pg_catalog.pg_class c
        CROSS JOIN LATERAL pg_catalog.aclexplode(c.relacl) acl
        WHERE c.oid = 'public.person_credential'::pg_catalog.regclass
        """;
    private const string ColumnGrantees = """
        SELECT DISTINCT CASE WHEN acl.grantee = 0 THEN 'PUBLIC' ELSE pg_catalog.pg_get_userbyid(acl.grantee) END
        FROM pg_catalog.pg_attribute a
        CROSS JOIN LATERAL pg_catalog.aclexplode(a.attacl) acl
        WHERE a.attrelid = 'public.person_credential'::pg_catalog.regclass AND a.attacl IS NOT NULL
        """;

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
        var scratch = await database.ScratchAsync();
        var onTable = await scratch.FirstColumnAsOwnerAsync(TableGrantees);
        var onColumns = await scratch.FirstColumnAsOwnerAsync(ColumnGrantees);

        Assert.DoesNotContain("PUBLIC", onTable.Concat(onColumns), StringComparer.Ordinal);
        Assert.Contains("app", onTable);
        Assert.Contains("app", onColumns);
    }

    [DatabaseFact]
    public async Task APublicGrantOnTheTableIsSeenWhoeverMadeIt()
    {
        var scratch = await database.ScratchAsync();
        await using (var granter = await database.BareRoleAsync())
        {
            await scratch.RunAsOwnerAsync($"GRANT SELECT ON public.person_credential TO {granter.Name} WITH GRANT OPTION");
            await granter.RunAsync("GRANT SELECT ON public.person_credential TO PUBLIC");

            Assert.Contains("PUBLIC", await scratch.FirstColumnAsOwnerAsync(TableGrantees), StringComparer.Ordinal);
        }

        Assert.DoesNotContain("PUBLIC", await scratch.FirstColumnAsOwnerAsync(TableGrantees), StringComparer.Ordinal);
    }

    [DatabaseFact]
    public async Task APublicGrantOnAColumnIsSeenWhoeverMadeIt()
    {
        var scratch = await database.ScratchAsync();
        await using (var granter = await database.BareRoleAsync())
        {
            await scratch.RunAsOwnerAsync($"GRANT SELECT (salt) ON public.person_credential TO {granter.Name} WITH GRANT OPTION");
            await granter.RunAsync("GRANT SELECT (salt) ON public.person_credential TO PUBLIC");

            Assert.Contains("PUBLIC", await scratch.FirstColumnAsOwnerAsync(ColumnGrantees), StringComparer.Ordinal);
        }

        Assert.DoesNotContain("PUBLIC", await scratch.FirstColumnAsOwnerAsync(ColumnGrantees), StringComparer.Ordinal);
    }
}
