using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Core.Persistence.Custody;
using Core.Persistence.People;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.People;

/// <summary>
/// The recomputation of a credential at sign-in is an event of the chain (#71, part 2, CA-4): the step credential_recomputed about the person, authored by the same person, written in the
/// transaction that changes the credential, and only when the credential changed.
/// </summary>
[Trait("Category", "Database")]
public sealed class PersonCredentialsRecomputeEventTests : IClassFixture<PeopleDatabase>
{
    // nosemgrep: secret-literal-csharp a synthetic password of this test, which hashes it into a throwaway database.
    private const string Password = "ZQX-SENHA-5521";
    private const int LowFactor = 2;
    private const int HighFactor = 3;

    private readonly PeopleDatabase database;

    public PersonCredentialsRecomputeEventTests(PeopleDatabase database)
    {
        this.database = database;
    }

    [DatabaseFact]
    public async Task AnEntryThatRecomputesLeavesOneEventAboutThePersonAuthoredByThePerson()
    {
        var person = await PersonWithAPasswordAsync();

        var entry = await (await database.CredentialsAsync(HighFactor)).VerifyAsync(person, Password, TestContext.Current.CancellationToken);

        Assert.Equal(PasswordOutcome.Accepted, entry);
        Assert.Equal($"{person}|person|{person}|administrative_act", await EventsOfAsync(person));
    }

    [DatabaseFact]
    public async Task AnEntryAtTheFactorTheCredentialHasAlreadyWritesNoEvent()
    {
        var person = await PersonWithAPasswordAsync();

        _ = await (await database.CredentialsAsync(LowFactor)).VerifyAsync(person, Password, TestContext.Current.CancellationToken);

        Assert.Equal(string.Empty, await EventsOfAsync(person));
    }

    [DatabaseFact]
    public async Task AWrongPasswordRecomputesNothingAndWritesNoEvent()
    {
        var person = await PersonWithAPasswordAsync();
        var before = (await database.CredentialOfAsync(person))!;

        var entry = await (await database.CredentialsAsync(HighFactor)).VerifyAsync(person, "ZQX-SENHA-0000", TestContext.Current.CancellationToken);

        var after = (await database.CredentialOfAsync(person))!;
        Assert.Equal((PasswordOutcome.Wrong, string.Empty, before.WorkFactor), (entry, await EventsOfAsync(person), after.WorkFactor));
    }

    [DatabaseFact]
    public async Task TheSecondEntryAtTheNewFactorWritesNoSecondEvent()
    {
        var person = await PersonWithAPasswordAsync();
        var credentials = await database.CredentialsAsync(HighFactor);

        _ = await credentials.VerifyAsync(person, Password, TestContext.Current.CancellationToken);
        _ = await credentials.VerifyAsync(person, Password, TestContext.Current.CancellationToken);

        Assert.Equal($"{person}|person|{person}|administrative_act", await EventsOfAsync(person));
    }

    private async Task<long> PersonWithAPasswordAsync()
    {
        var registry = await database.RegistryAsync();
        var person = PeopleDatabase.Id(await registry.CreateAsync(new NewPerson(PeopleDatabase.NewName(), PersonFunctions.Accepted[0]), CustodyAuthor.System, TestContext.Current.CancellationToken));
        _ = await (await database.CredentialsAsync(LowFactor)).SetPasswordAsync(person, Password, CustodyAuthor.System, TestContext.Current.CancellationToken);
        return person;
    }

    [SuppressMessage("Security", "CA2100", Justification = "The statement is a literal of this class; the identifier is a parameter.")]
    private async Task<string> EventsOfAsync(long person)
    {
        await using var connection = await Sql.OpenAsync((await database.ScratchAsync()).OwnerConnectionString);
        await using var command = new NpgsqlCommand(
            "SELECT coalesce(string_agg(concat_ws('|', item_id, item_kind, actor_person_id, reason), ';'), '') FROM public.custody_event WHERE step = 'credential_recomputed' AND item_id = @id",
            connection);
        _ = command.Parameters.AddWithValue("id", person);
        return Convert.ToString(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken), CultureInfo.InvariantCulture)!;
    }
}
