using Core.Persistence.Custody;
using Core.Persistence.People;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.People;

/// <summary>
/// What the server itself holds, whatever the code does (#109, CA-3 and CA-4): a person is never deleted and the history is only added to,
/// refused to the application's role and to the role that owns the tables alike.
/// </summary>
[Trait("Category", "Database")]
public sealed class PersonHistoryGuaranteeTests : IClassFixture<PeopleDatabase>
{
    private const string InsufficientPrivilege = PostgresErrorCodes.InsufficientPrivilege;

    private readonly PeopleDatabase database;

    public PersonHistoryGuaranteeTests(PeopleDatabase database)
    {
        this.database = database;
    }

    [DatabaseFact]
    public async Task DeletingAPersonIsRefusedToTheApplicationAndKeepsTheRow()
    {
        var id = await NewPersonAsync();

        var refused = await database.RefusedToAppAsync($"DELETE FROM public.person WHERE id = {id}");

        Assert.Equal(InsufficientPrivilege, refused.SqlState);
        Assert.NotNull(await database.PersonAsync(id));
    }

    [DatabaseFact]
    public async Task DeletingAPersonIsRefusedToTheOwnerToo()
    {
        var id = await NewPersonAsync();

        var refused = await database.RefusedToOwnerAsync($"DELETE FROM public.person WHERE id = {id}");

        Assert.Equal(InsufficientPrivilege, refused.SqlState);
        Assert.NotNull(await database.PersonAsync(id));
    }

    [DatabaseFact]
    public async Task DeletingADeactivatedPersonIsRefusedAndTheHistoryStillPointsAtThem()
    {
        var registry = await database.RegistryAsync();
        var id = await NewPersonAsync();
        _ = PeopleDatabase.Id(await registry.DeactivateAsync(id, CustodyAuthor.System, TestContext.Current.CancellationToken));

        var refused = await database.RefusedToAppAsync($"DELETE FROM public.person WHERE id = {id}");

        Assert.Equal(InsufficientPrivilege, refused.SqlState);
        Assert.False((await database.PersonAsync(id))!.Active);
        Assert.Equal(2, (await database.ChangesOfAsync(id)).Count);
    }

    [DatabaseFact]
    public async Task EmptyingThePersonTableIsRefusedToTheOwner()
    {
        var id = await NewPersonAsync();

        var refused = await database.RefusedToOwnerAsync("TRUNCATE public.person CASCADE");

        Assert.Equal(InsufficientPrivilege, refused.SqlState);
        Assert.NotNull(await database.PersonAsync(id));
    }

    [DatabaseFact]
    public async Task ApplicationChangesNoColumnOfAPersonButTheFunctionAndTheState()
    {
        var id = await NewPersonAsync();

        foreach (var column in new[] { "name = 'zqx-outro'", "created_by = 'zqx'", "created_at = now()" })
        {
            var refused = await database.RefusedToAppAsync($"UPDATE public.person SET {column} WHERE id = {id}");

            Assert.Equal(InsufficientPrivilege, refused.SqlState);
        }
    }

    [DatabaseFact]
    public async Task ApplicationCannotWriteWhoOrWhenOfAPersonOrOfAChange()
    {
        var id = await NewPersonAsync();

        foreach (var statement in new[]
        {
            "INSERT INTO public.person (name, function_name, created_by) VALUES ('zqx-forjada-1', 'Anotação', 'zqx')",
            "INSERT INTO public.person (name, function_name, created_at) VALUES ('zqx-forjada-2', 'Anotação', now())",
            $"INSERT INTO public.person_change (person_id, kind, changed_by) VALUES ({id}, 'deactivated', 'zqx')",
            $"INSERT INTO public.person_change (person_id, kind, changed_at) VALUES ({id}, 'deactivated', now())",
        })
        {
            var refused = await database.RefusedToAppAsync(statement);

            Assert.Equal(InsufficientPrivilege, refused.SqlState);
        }

        _ = Assert.Single(await database.ChangesOfAsync(id));
    }

    [DatabaseFact]
    public async Task UpdatingAChangeRecordIsRefusedToTheApplicationAndToTheOwner()
    {
        var id = await NewPersonAsync();

        foreach (var refused in new[]
        {
            await database.RefusedToAppAsync($"UPDATE public.person_change SET kind = 'deactivated' WHERE person_id = {id}"),
            await database.RefusedToOwnerAsync($"UPDATE public.person_change SET kind = 'deactivated' WHERE person_id = {id}"),
            await database.RefusedToOwnerAsync($"UPDATE public.person_change SET changed_by = 'zqx' WHERE person_id = {id}"),
        })
        {
            Assert.Equal(InsufficientPrivilege, refused.SqlState);
        }

        Assert.Equal("created", Assert.Single(await database.ChangesOfAsync(id)).Kind);
    }

    [DatabaseFact]
    public async Task DeletingAChangeRecordIsRefusedToTheApplicationAndToTheOwner()
    {
        var id = await NewPersonAsync();

        foreach (var refused in new[]
        {
            await database.RefusedToAppAsync($"DELETE FROM public.person_change WHERE person_id = {id}"),
            await database.RefusedToOwnerAsync($"DELETE FROM public.person_change WHERE person_id = {id}"),
            await database.RefusedToOwnerAsync("TRUNCATE public.person_change"),
        })
        {
            Assert.Equal(InsufficientPrivilege, refused.SqlState);
        }

        _ = Assert.Single(await database.ChangesOfAsync(id));
    }

    [DatabaseFact]
    public async Task ARecordOfChangeCannotPointAtAPersonNobodyRegistered()
    {
        var refused = await database.RefusedToAppAsync("INSERT INTO public.person_change (person_id, kind) VALUES (9223372036854775807, 'created')");

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, refused.SqlState);
    }

    [DatabaseFact]
    public async Task ARecordOfChangeNamesOneOfThreeKindsAndNoOther()
    {
        var id = await NewPersonAsync();

        var refused = await database.RefusedToAppAsync($"INSERT INTO public.person_change (person_id, kind) VALUES ({id}, 'deleted')");

        Assert.Equal(PostgresErrorCodes.CheckViolation, refused.SqlState);
    }

    [DatabaseFact]
    public async Task TheServerRefusesANameOrAFunctionThatIsEmptyOrPaddedEvenWhenWrittenAround()
    {
        foreach (var (name, function) in new[] { ("", "Anotação"), (" zqx-x", "Anotação"), ("zqx-x ", "Anotação"), ("zqx-x", ""), ("zqx-x", " Anotação"), ("zqx-x", "Anotação ") })
        {
            var refused = await database.RefusedToOwnerAsync($"INSERT INTO public.person (name, function_name) VALUES ('{name}', '{function}')");

            Assert.Equal(PostgresErrorCodes.CheckViolation, refused.SqlState);
        }
    }

    private async Task<long> NewPersonAsync()
    {
        var registry = await database.RegistryAsync();
        return PeopleDatabase.Id(await registry.CreateAsync(new NewPerson(PeopleDatabase.NewName(), PersonFunctions.Accepted[0]), CustodyAuthor.System, TestContext.Current.CancellationToken));
    }
}
