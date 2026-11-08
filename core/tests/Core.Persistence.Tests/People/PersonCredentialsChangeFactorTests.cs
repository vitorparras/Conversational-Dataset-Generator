using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Core.Persistence.Custody;
using Core.Persistence.People;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.People;

/// <summary>
/// The change of the own password derives the new one with the work factor in force (#597, CA-3): a credential stored at another factor is replaced by one at the factor of the derivation
/// the store was given, the old password stops opening, the new one opens, and the act is the step password_changed, authored by the person, with the count of sessions as the reference.
/// </summary>
[Trait("Category", "Database")]
public sealed class PersonCredentialsChangeFactorTests : IClassFixture<PeopleDatabase>
{
    // nosemgrep: secret-literal-csharp a synthetic password of this test, which hashes it into a throwaway database.
    private const string Old = "ZQX-SENHA-ANTIGA-3310";

    // nosemgrep: secret-literal-csharp a synthetic password of this test, which hashes it into a throwaway database.
    private const string Fresh = "ZQX-SENHA-NOVA-8842";
    private const int StoredFactor = 2;
    private const int CurrentFactor = 5;

    private readonly PeopleDatabase database;

    public PersonCredentialsChangeFactorTests(PeopleDatabase database)
    {
        this.database = database;
    }

    [DatabaseFact]
    public async Task TheCredentialWrittenByTheChangeHasTheFactorOfTheConfigurationAndNotTheStoredOne()
    {
        var person = await PersonWithAPasswordAsync();
        Assert.Equal(StoredFactor, (await database.CredentialOfAsync(person))!.WorkFactor);

        var changed = await (await database.CredentialsAsync(CurrentFactor)).ChangeOwnPasswordAsync(person, Fresh, 3, TestContext.Current.CancellationToken);

        Assert.Equal(PersonOutcome.Done, changed.Outcome);
        Assert.Equal(CurrentFactor, (await database.CredentialOfAsync(person))!.WorkFactor);
    }

    [DatabaseFact]
    public async Task TheNewPasswordOpensTheOldOneNoLongerAndTheEventSaysWhoAndHowMany()
    {
        var person = await PersonWithAPasswordAsync();
        var credentials = await database.CredentialsAsync(CurrentFactor);

        _ = await credentials.ChangeOwnPasswordAsync(person, Fresh, 3, TestContext.Current.CancellationToken);

        Assert.Equal(PasswordOutcome.Accepted, await credentials.VerifyAsync(person, Fresh, TestContext.Current.CancellationToken));
        Assert.Equal(PasswordOutcome.Wrong, await credentials.VerifyAsync(person, Old, TestContext.Current.CancellationToken));
        Assert.Equal($"{person}|person|{person}|administrative_act|3", await EventsOfAsync(person));
    }

    [DatabaseFact]
    public async Task AnEmptyNewPasswordChangesNothingAndWritesNoEvent()
    {
        var person = await PersonWithAPasswordAsync();

        var refused = await (await database.CredentialsAsync(CurrentFactor)).ChangeOwnPasswordAsync(person, string.Empty, 0, TestContext.Current.CancellationToken);

        Assert.Equal(PersonOutcome.PasswordInvalid, refused.Outcome);
        Assert.Equal((StoredFactor, string.Empty), ((await database.CredentialOfAsync(person))!.WorkFactor, await EventsOfAsync(person)));
    }

    private async Task<long> PersonWithAPasswordAsync()
    {
        var registry = await database.RegistryAsync();
        var person = PeopleDatabase.Id(await registry.CreateAsync(new NewPerson(PeopleDatabase.NewName(), PersonFunctions.Accepted[0]), CustodyAuthor.System, TestContext.Current.CancellationToken));
        _ = await (await database.CredentialsAsync(StoredFactor)).SetPasswordAsync(person, Old, CustodyAuthor.System, TestContext.Current.CancellationToken);
        return person;
    }

    [SuppressMessage("Security", "CA2100", Justification = "The statement is a literal of this class; the identifier is a parameter.")]
    private async Task<string> EventsOfAsync(long person)
    {
        await using var connection = await Sql.OpenAsync((await database.ScratchAsync()).OwnerConnectionString);
        await using var command = new NpgsqlCommand(
            "SELECT coalesce(string_agg(concat_ws('|', item_id, item_kind, actor_person_id, reason, reference), ';'), '') FROM public.custody_event WHERE step = 'password_changed' AND item_id = @id",
            connection);
        _ = command.Parameters.AddWithValue("id", person);
        return Convert.ToString(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken), CultureInfo.InvariantCulture)!;
    }
}
