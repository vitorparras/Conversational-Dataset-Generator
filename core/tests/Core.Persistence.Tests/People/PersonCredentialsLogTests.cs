using Core.Persistence.Custody;
using Core.Persistence.People;
using Core.Persistence.Tests.Fixtures;

namespace Core.Persistence.Tests.People;

/// <summary>
/// What the records of an entry that is accepted and of one that is refused hold (#69, CA-4): the driver logs at every level
/// while the passwords are set and checked, and neither the password, nor the salt, nor the result appears in what it wrote,
/// in what the operation answered or in what the history of the person holds, in text, in hex or in base64.
/// </summary>
[Trait("Category", "Database")]
public sealed class PersonCredentialsLogTests : IClassFixture<PeopleDatabase>
{
    private const string Password = "ZQX-SENHA-3381";
    private const string WrongPassword = "ZQX-SENHA-0042";
    private const int LowFactor = 2;
    private const int HighFactor = 5;

    private static readonly string Annotation = PersonFunctions.Accepted[0];

    private readonly PeopleDatabase database;

    public PersonCredentialsLogTests(PeopleDatabase database)
    {
        this.database = database;
    }

    [DatabaseFact]
    public async Task AcceptedEntryLeavesNeitherThePasswordNorTheSaltNorTheResultInTheRecords()
    {
        var id = await NewPersonAsync();
        await using var logged = await database.LoggedCredentialsAsync(LowFactor);

        var set = await logged.Store.SetPasswordAsync(id, Password, CustodyAuthor.System, TestContext.Current.CancellationToken);
        var entry = await logged.Store.VerifyAsync(id, Password, TestContext.Current.CancellationToken);

        Assert.Equal(PasswordOutcome.Accepted, entry);
        await AssertNothingInTheRecordsAsync(id, logged.Log, $"{set} {entry}");
    }

    [DatabaseFact]
    public async Task AcceptedEntryThatRecomputesLeavesNothingOfEitherDerivationInTheRecords()
    {
        var id = await NewPersonAsync();
        _ = await (await database.CredentialsAsync(LowFactor)).SetPasswordAsync(id, Password, CustodyAuthor.System, TestContext.Current.CancellationToken);
        var before = (await database.CredentialOfAsync(id))!;
        await using var logged = await database.LoggedCredentialsAsync(HighFactor);

        var entry = await logged.Store.VerifyAsync(id, Password, TestContext.Current.CancellationToken);

        var after = (await database.CredentialOfAsync(id))!;
        Assert.Equal((PasswordOutcome.Accepted, HighFactor), (entry, after.WorkFactor));
        Assert.NotEqual(before.Derived, after.Derived);
        await AssertNothingInTheRecordsAsync(id, logged.Log, entry.ToString(), before);
    }

    [DatabaseFact]
    public async Task EntryWithTheWrongPasswordLeavesNothingInTheRecords()
    {
        var id = await NewPersonAsync();
        await using var logged = await database.LoggedCredentialsAsync(LowFactor);
        _ = await logged.Store.SetPasswordAsync(id, Password, CustodyAuthor.System, TestContext.Current.CancellationToken);

        var entry = await logged.Store.VerifyAsync(id, WrongPassword, TestContext.Current.CancellationToken);

        Assert.Equal(PasswordOutcome.Wrong, entry);
        await AssertNothingInTheRecordsAsync(id, logged.Log, entry.ToString());
    }

    [DatabaseFact]
    public async Task EntryOfADeactivatedPersonWithTheRightPasswordLeavesNothingInTheRecords()
    {
        var id = await NewPersonAsync();
        await using var logged = await database.LoggedCredentialsAsync(LowFactor);
        _ = await logged.Store.SetPasswordAsync(id, Password, CustodyAuthor.System, TestContext.Current.CancellationToken);
        _ = await (await database.RegistryAsync()).DeactivateAsync(id, CustodyAuthor.System, TestContext.Current.CancellationToken);

        var entry = await logged.Store.VerifyAsync(id, Password, TestContext.Current.CancellationToken);

        Assert.Equal(PasswordOutcome.Deactivated, entry);
        await AssertNothingInTheRecordsAsync(id, logged.Log, entry.ToString());
    }

    [DatabaseFact]
    public async Task EntryOfAPersonWithoutPasswordAndOfOneWhoDoesNotExistLeavesNothingInTheRecords()
    {
        var id = await NewPersonAsync();
        await using var logged = await database.LoggedCredentialsAsync(LowFactor);

        var without = await logged.Store.VerifyAsync(id, Password, TestContext.Current.CancellationToken);
        var unknown = await logged.Store.VerifyAsync(long.MaxValue, Password, TestContext.Current.CancellationToken);

        Assert.Equal((PasswordOutcome.NoPassword, PasswordOutcome.Unknown), (without, unknown));
        await AssertNothingInTheRecordsAsync(id, logged.Log, $"{without} {unknown}");
    }

    [DatabaseFact]
    public async Task PasswordSetRefusedForADeactivatedPersonLeavesNothingInTheRecords()
    {
        var id = await NewPersonAsync();
        _ = await (await database.RegistryAsync()).DeactivateAsync(id, CustodyAuthor.System, TestContext.Current.CancellationToken);
        await using var logged = await database.LoggedCredentialsAsync(LowFactor);

        var set = await logged.Store.SetPasswordAsync(id, Password, CustodyAuthor.System, TestContext.Current.CancellationToken);

        Assert.Equal(PersonOutcome.Deactivated, set.Outcome);
        await AssertNothingInTheRecordsAsync(id, logged.Log, $"{set.Outcome} {set.Refusal}");
    }

    private async Task AssertNothingInTheRecordsAsync(long id, CapturedNpgsqlLog log, string answered, params PeopleDatabase.CredentialRow[] earlier)
    {
        var history = string.Join(Environment.NewLine, (await database.ChangesOfAsync(id)).Select(change => change.ToString()));
        var records = string.Join(Environment.NewLine, log.Everything, answered, history);
        var rows = earlier.Append(await database.CredentialOfAsync(id)).OfType<PeopleDatabase.CredentialRow>();
        var secrets = SecretEncodings.Of(Password).Concat(SecretEncodings.Of(WrongPassword))
            .Concat(rows.SelectMany(row => SecretEncodings.Of(row.Salt).Concat(SecretEncodings.Of(row.Derived))));

        Assert.Contains("person_credential", log.Everything, StringComparison.Ordinal);
        SecretEncodings.AssertNoneIn(records, secrets);
        SecretEncodings.AssertNoByteaIn(records);
    }

    private async Task<long> NewPersonAsync() =>
        PeopleDatabase.Id(await (await database.RegistryAsync()).CreateAsync(new NewPerson(PeopleDatabase.NewName(), Annotation), CustodyAuthor.System, TestContext.Current.CancellationToken));
}
