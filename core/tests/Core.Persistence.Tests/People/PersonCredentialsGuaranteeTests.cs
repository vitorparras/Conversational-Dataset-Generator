using System.Globalization;
using Core.Persistence.Custody;
using Core.Persistence.People;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.People;

/// <summary>What the server itself holds of a credential, whatever the code does (#69): the application replaces the derivation and nothing else, and nobody empties the table.</summary>
[Trait("Category", "Database")]
public sealed class PersonCredentialsGuaranteeTests : IClassFixture<PeopleDatabase>
{
    private const string InsufficientPrivilege = PostgresErrorCodes.InsufficientPrivilege;
    private const string Password = "ZQX-SENHA-3381";
    private const string Salt = "decode('00000000000000000000000000000000', 'hex')";
    private const string Result = "decode('00', 'hex')";

    private readonly PeopleDatabase database;

    public PersonCredentialsGuaranteeTests(PeopleDatabase database)
    {
        this.database = database;
    }

    [DatabaseFact]
    public async Task ApplicationCannotMoveACredentialToAnotherPersonNorChangeWhoWroteIt()
    {
        var id = await NewPersonWithPasswordAsync();
        var other = await NewPersonWithPasswordAsync();

        foreach (var column in new[] { $"person_id = {other}", "created_by = 'zqx'" })
        {
            var refused = await database.RefusedToAppAsync($"UPDATE public.person_credential SET {column} WHERE person_id = {id}");

            Assert.Equal(InsufficientPrivilege, refused.SqlState);
        }

        Assert.NotNull(await database.CredentialOfAsync(id));
    }

    [DatabaseFact]
    public async Task ApplicationCannotWriteWhoOrWhenOfACredentialAtInsertion()
    {
        var id = await NewPersonAsync();

        foreach (var written in new[] { "created_by) VALUES ({0}, 'zqx', 1, {1}, {2}, 'zqx'", "derived_at) VALUES ({0}, 'zqx', 1, {1}, {2}, now()" })
        {
            var refused = await database.RefusedToAppAsync(
                "INSERT INTO public.person_credential (person_id, algorithm, work_factor, salt, derived, " + string.Format(CultureInfo.InvariantCulture, written, id, Salt, Result) + ")");

            Assert.Equal(InsufficientPrivilege, refused.SqlState);
        }

        Assert.Null(await database.CredentialOfAsync(id));
    }

    [DatabaseFact]
    public async Task EmptyingTheCredentialTableIsRefusedToTheApplicationAndToTheOwner()
    {
        var id = await NewPersonWithPasswordAsync();

        foreach (var refused in new[]
        {
            await database.RefusedToAppAsync("TRUNCATE public.person_credential"),
            await database.RefusedToOwnerAsync("TRUNCATE public.person_credential"),
        })
        {
            Assert.Equal(InsufficientPrivilege, refused.SqlState);
        }

        Assert.NotNull(await database.CredentialOfAsync(id));
    }

    private async Task<long> NewPersonAsync()
    {
        var person = new NewPerson(PeopleDatabase.NewName(), PersonFunctions.Accepted[0]);
        return PeopleDatabase.Id(await (await database.RegistryAsync()).CreateAsync(person, CustodyAuthor.System, TestContext.Current.CancellationToken));
    }

    private async Task<long> NewPersonWithPasswordAsync()
    {
        var id = await NewPersonAsync();
        _ = PeopleDatabase.Id(await (await database.CredentialsAsync(1)).SetPasswordAsync(id, Password, CustodyAuthor.System, TestContext.Current.CancellationToken));
        return id;
    }
}
