using System.Diagnostics.CodeAnalysis;
using Core.Persistence.Custody;
using Core.Persistence.People;
using Core.Persistence.Tests.Fixtures;
using Core.Persistence.Tests.ServerGuarantees;
using Npgsql;

namespace Core.Persistence.Tests.People;

/// <summary>
/// The e-mail address of a person who operates (issue 635), against a real server: it is optional, kept, replaced and removed; the database removes it when the person is deactivated
/// (policy 009); it refuses one that is no address; and nothing of it reaches the log of the driver, the history of the person or the chain of custody (CA-5).
/// </summary>
[Trait("Category", "Database")]
public sealed class PersonEmailsTests : IClassFixture<PeopleDatabase>
{
    private const string Address = "zqx-pessoa@example.test";
    private const string Everything =
        """
        SELECT coalesce((SELECT string_agg(pg_catalog.to_jsonb(c)::text, '') FROM public.person_change c), '')
            || coalesce((SELECT string_agg(pg_catalog.to_jsonb(e)::text, '') FROM public.custody_event e), '')
        """;

    private static readonly string Annotation = PersonFunctions.Accepted[0];

    private readonly PeopleDatabase database;

    public PersonEmailsTests(PeopleDatabase database)
    {
        this.database = database;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [DatabaseFact]
    public async Task APersonHasNoAddressUntilOneIsKeptAndANewOneTakesThePlaceOfTheOld()
    {
        var (emails, id) = await NewPersonAsync();

        var before = await emails.AddressOfAsync(id, Token);
        await emails.SetAsync(id, Address, Token);
        await emails.SetAsync(id, "zqx-outra@example.test", Token);

        Assert.Null(before);
        Assert.Equal("zqx-outra@example.test", await emails.AddressOfAsync(id, Token));
    }

    [DatabaseFact]
    public async Task NoAddressRemovesTheOneKept()
    {
        var (emails, id) = await NewPersonAsync();
        await emails.SetAsync(id, Address, Token);

        await emails.SetAsync(id, address: null, Token);

        Assert.Null(await emails.AddressOfAsync(id, Token));
    }

    [DatabaseFact]
    public async Task TheDatabaseRemovesTheAddressWhenThePersonIsDeactivated()
    {
        var (emails, id) = await NewPersonAsync();
        await emails.SetAsync(id, Address, Token);

        _ = await (await database.RegistryAsync()).DeactivateAsync(id, CustodyAuthor.System, Token);

        Assert.Null(await emails.AddressOfAsync(id, Token));
    }

    [DatabaseFact]
    public async Task TheDatabaseRefusesWhatIsNoAddressEvenWhenTheCodeIsPassedBy()
    {
        var (_, id) = await NewPersonAsync();
        var source = await database.ApplicationAsync();

        _ = await Assert.ThrowsAsync<PostgresException>(() => InsertAsync(source, id, "zqx sem arroba"));
    }

    [DatabaseFact]
    public async Task Ca5TheAddressReachesNeitherTheLogOfTheDriverNorTheHistoryNorTheChain()
    {
        var (_, id) = await NewPersonAsync();
        var log = new CapturedNpgsqlLog();
        var builder = new NpgsqlDataSourceBuilder((await database.ScratchAsync()).AppConnectionString);
        _ = builder.UseLoggerFactory(log);
        await using (var logged = builder.Build())
        {
            await new PersonEmails(logged).SetAsync(id, Address, Token);
            _ = await new PersonRegistry(logged).DeactivateAsync(id, CustodyAuthor.System, Token);
        }

        log.Dispose();
        Assert.DoesNotContain(Address, log.Everything, StringComparison.Ordinal);
        Assert.DoesNotContain(Address, await OwnerTextAsync(Everything), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("zqx")]
    [InlineData("zqx@")]
    [InlineData("@example.test")]
    [InlineData("zqx pessoa@example.test")]
    [InlineData("zqx@@example.test")]
    [InlineData("Zqx <zqx@example.test>")]
    public async Task WhatIsNoAddressIsRefusedBeforeTheServerIsAsked(string typed)
    {
        await using var server = new UnreachableServer();

        Assert.False(PersonEmails.IsAddress(typed));
        _ = await Assert.ThrowsAsync<ArgumentException>(() => new PersonEmails(server.DataSource).SetAsync(1, typed, Token));
    }

    [Fact]
    public void AnAddressAsAPersonTypesItIsTaken() => Assert.True(PersonEmails.IsAddress("zqx.pessoa+lotes@example.test"));

    private async Task<(PersonEmails Emails, long Id)> NewPersonAsync()
    {
        var registry = await database.RegistryAsync();
        var id = PeopleDatabase.Id(await registry.CreateAsync(new NewPerson(PeopleDatabase.NewName(), Annotation), CustodyAuthor.System, Token));
        return (new PersonEmails(await database.ApplicationAsync()), id);
    }

    [SuppressMessage("Security", "CA2100", Justification = "The statement is a literal of this class, never input.")]
    private static async Task InsertAsync(NpgsqlDataSource source, long id, string address)
    {
        await using var command = source.CreateCommand("INSERT INTO public.person_email (person_id, address) VALUES (@person, @address)");
        _ = command.Parameters.AddWithValue("person", id);
        _ = command.Parameters.AddWithValue("address", address);
        _ = await command.ExecuteNonQueryAsync(Token);
    }

    [SuppressMessage("Security", "CA2100", Justification = "The statement is a literal of this class, never input.")]
    private async Task<string> OwnerTextAsync(string statement)
    {
        await using var connection = await Sql.OpenAsync((await database.ScratchAsync()).OwnerConnectionString);
        await using var command = new NpgsqlCommand(statement, connection);
        return (string)(await command.ExecuteScalarAsync(Token))!;
    }
}
