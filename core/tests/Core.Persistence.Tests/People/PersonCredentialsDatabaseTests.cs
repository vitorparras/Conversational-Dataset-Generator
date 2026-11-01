using Core.Persistence.Custody;
using Core.Persistence.People;
using Core.Persistence.Tests.Fixtures;

namespace Core.Persistence.Tests.People;

/// <summary>
/// The passwords against a real server, as the application's role (#69, CA-1 and CA-3): what is stored,
/// that two people with one password differ, and the recomputation at the next entry.
/// </summary>
[Trait("Category", "Database")]
public sealed class PersonCredentialsDatabaseTests : IClassFixture<PeopleDatabase>
{
    private const string Password = "ZQX-SENHA-6620";
    private const int LowFactor = 2;
    private const int HighFactor = 5;

    private static readonly string Annotation = PersonFunctions.Accepted[0];

    private readonly PeopleDatabase database;

    public PersonCredentialsDatabaseTests(PeopleDatabase database)
    {
        this.database = database;
    }

    [DatabaseFact]
    public async Task PasswordSetIsStoredAsTheDerivationWithTheFactorAndNeverAsThePassword()
    {
        var id = await NewPersonAsync();

        _ = PeopleDatabase.Id(await (await database.CredentialsAsync(LowFactor)).SetPasswordAsync(id, Password, CustodyAuthor.System, TestContext.Current.CancellationToken));

        var row = (await database.CredentialOfAsync(id))!;
        Assert.Equal(("PBKDF2-HMAC-SHA256", LowFactor), (row.Algorithm, row.WorkFactor));
        Assert.True(row.Salt.Length >= 16);
        Assert.DoesNotContain(System.Text.Encoding.UTF8.GetBytes(Password), row.Derived);
    }

    [DatabaseFact]
    public async Task TwoPeopleWithTheSamePasswordHaveAnotherSaltAndAnotherResult()
    {
        var credentials = await database.CredentialsAsync(LowFactor);
        var first = await NewPersonAsync();
        var second = await NewPersonAsync();

        _ = await credentials.SetPasswordAsync(first, Password, CustodyAuthor.System, TestContext.Current.CancellationToken);
        _ = await credentials.SetPasswordAsync(second, Password, CustodyAuthor.System, TestContext.Current.CancellationToken);

        var (one, other) = ((await database.CredentialOfAsync(first))!, (await database.CredentialOfAsync(second))!);
        Assert.NotEqual(one.Salt, other.Salt);
        Assert.NotEqual(one.Derived, other.Derived);
    }

    [DatabaseFact]
    public async Task SettingThePasswordIsRecordedInTheHistoryWithTheActorAndNothingOfThePassword()
    {
        var actor = await NewPersonAsync();
        var id = await NewPersonAsync();

        _ = await (await database.CredentialsAsync(LowFactor)).SetPasswordAsync(id, Password, CustodyAuthor.Person(actor), TestContext.Current.CancellationToken);

        var change = (await database.ChangesOfAsync(id)).Single(row => row.Kind == "password_set");
        Assert.Equal(actor, change.ActorId);
        Assert.Null(change.PreviousFunction);
        Assert.Null(change.NewFunction);
    }

    [DatabaseFact]
    public async Task SettingThePasswordAgainReplacesTheDerivation()
    {
        var credentials = await database.CredentialsAsync(LowFactor);
        var id = await NewPersonAsync();
        _ = await credentials.SetPasswordAsync(id, Password, CustodyAuthor.System, TestContext.Current.CancellationToken);
        var before = (await database.CredentialOfAsync(id))!;

        _ = await credentials.SetPasswordAsync(id, Password + "x", CustodyAuthor.System, TestContext.Current.CancellationToken);

        var after = (await database.CredentialOfAsync(id))!;
        Assert.NotEqual(before.Derived, after.Derived);
        Assert.Equal(PasswordOutcome.Accepted, await credentials.VerifyAsync(id, Password + "x", TestContext.Current.CancellationToken));
        Assert.Equal(PasswordOutcome.Wrong, await credentials.VerifyAsync(id, Password, TestContext.Current.CancellationToken));
    }

    [DatabaseFact]
    public async Task PasswordOfADeactivatedPersonIsRefusedAndWritesNothing()
    {
        var registry = await database.RegistryAsync();
        var id = await NewPersonAsync();
        _ = await registry.DeactivateAsync(id, CustodyAuthor.System, TestContext.Current.CancellationToken);

        var result = await (await database.CredentialsAsync(LowFactor)).SetPasswordAsync(id, Password, CustodyAuthor.System, TestContext.Current.CancellationToken);

        Assert.Equal(PersonOutcome.Deactivated, result.Outcome);
        Assert.Null(await database.CredentialOfAsync(id));
    }

    [DatabaseFact]
    public async Task PasswordOfAPersonWhoDoesNotExistIsRefused()
    {
        var result = await (await database.CredentialsAsync(LowFactor)).SetPasswordAsync(long.MaxValue, Password, CustodyAuthor.System, TestContext.Current.CancellationToken);

        Assert.Equal(PersonOutcome.NotFound, result.Outcome);
    }

    [DatabaseFact]
    public async Task PasswordSetByADeactivatedActorIsRefusedAndWritesNothing()
    {
        var registry = await database.RegistryAsync();
        var actor = await NewPersonAsync();
        var id = await NewPersonAsync();
        _ = await registry.DeactivateAsync(actor, CustodyAuthor.System, TestContext.Current.CancellationToken);

        var result = await (await database.CredentialsAsync(LowFactor)).SetPasswordAsync(id, Password, CustodyAuthor.Person(actor), TestContext.Current.CancellationToken);

        Assert.Equal(PersonOutcome.ActorDeactivated, result.Outcome);
        Assert.Null(await database.CredentialOfAsync(id));
    }

    [DatabaseFact]
    public async Task RightPasswordIsAcceptedAndWrongOneIsNot()
    {
        var credentials = await database.CredentialsAsync(LowFactor);
        var id = await NewPersonAsync();
        _ = await credentials.SetPasswordAsync(id, Password, CustodyAuthor.System, TestContext.Current.CancellationToken);

        Assert.Equal(PasswordOutcome.Accepted, await credentials.VerifyAsync(id, Password, TestContext.Current.CancellationToken));
        Assert.Equal(PasswordOutcome.Wrong, await credentials.VerifyAsync(id, Password + "x", TestContext.Current.CancellationToken));
    }

    [DatabaseFact]
    public async Task PersonWithoutPasswordAndPersonWhoDoesNotExistAreTold()
    {
        var credentials = await database.CredentialsAsync(LowFactor);
        var id = await NewPersonAsync();

        Assert.Equal(PasswordOutcome.NoPassword, await credentials.VerifyAsync(id, Password, TestContext.Current.CancellationToken));
        Assert.Equal(PasswordOutcome.Unknown, await credentials.VerifyAsync(long.MaxValue, Password, TestContext.Current.CancellationToken));
    }

    [DatabaseFact]
    public async Task DeactivatedPersonIsRefusedEvenWithTheRightPassword()
    {
        var registry = await database.RegistryAsync();
        var credentials = await database.CredentialsAsync(LowFactor);
        var id = await NewPersonAsync();
        _ = await credentials.SetPasswordAsync(id, Password, CustodyAuthor.System, TestContext.Current.CancellationToken);
        _ = await registry.DeactivateAsync(id, CustodyAuthor.System, TestContext.Current.CancellationToken);

        Assert.Equal(PasswordOutcome.Deactivated, await credentials.VerifyAsync(id, Password, TestContext.Current.CancellationToken));
    }

    [DatabaseFact]
    public async Task RightPasswordWithALowerStoredFactorIsAcceptedAndTheRowTakesTheCurrentFactor()
    {
        var id = await NewPersonAsync();
        _ = await (await database.CredentialsAsync(LowFactor)).SetPasswordAsync(id, Password, CustodyAuthor.System, TestContext.Current.CancellationToken);
        var before = (await database.CredentialOfAsync(id))!;

        var outcome = await (await database.CredentialsAsync(HighFactor)).VerifyAsync(id, Password, TestContext.Current.CancellationToken);

        var after = (await database.CredentialOfAsync(id))!;
        Assert.Equal(PasswordOutcome.Accepted, outcome);
        Assert.Equal(HighFactor, after.WorkFactor);
        Assert.NotEqual(before.Derived, after.Derived);
        Assert.Equal(PasswordOutcome.Accepted, await (await database.CredentialsAsync(HighFactor)).VerifyAsync(id, Password, TestContext.Current.CancellationToken));
    }

    [DatabaseFact]
    public async Task WrongPasswordWithALowerStoredFactorDoesNotRecompute()
    {
        var id = await NewPersonAsync();
        _ = await (await database.CredentialsAsync(LowFactor)).SetPasswordAsync(id, Password, CustodyAuthor.System, TestContext.Current.CancellationToken);
        var before = (await database.CredentialOfAsync(id))!;

        var outcome = await (await database.CredentialsAsync(HighFactor)).VerifyAsync(id, Password + "x", TestContext.Current.CancellationToken);

        var after = (await database.CredentialOfAsync(id))!;
        Assert.Equal(PasswordOutcome.Wrong, outcome);
        Assert.Equal(LowFactor, after.WorkFactor);
        Assert.Equal(before.Derived, after.Derived);
    }

    [DatabaseFact]
    public async Task RightPasswordWithAHigherStoredFactorKeepsTheRow()
    {
        var id = await NewPersonAsync();
        _ = await (await database.CredentialsAsync(HighFactor)).SetPasswordAsync(id, Password, CustodyAuthor.System, TestContext.Current.CancellationToken);
        var before = (await database.CredentialOfAsync(id))!;

        var outcome = await (await database.CredentialsAsync(LowFactor)).VerifyAsync(id, Password, TestContext.Current.CancellationToken);

        var after = (await database.CredentialOfAsync(id))!;
        Assert.Equal(PasswordOutcome.Accepted, outcome);
        Assert.Equal(HighFactor, after.WorkFactor);
        Assert.Equal(before.Derived, after.Derived);
    }

    [DatabaseFact]
    public async Task DeletingACredentialIsRefusedToTheApplicationAndToTheOwner()
    {
        var id = await NewPersonAsync();
        _ = await (await database.CredentialsAsync(LowFactor)).SetPasswordAsync(id, Password, CustodyAuthor.System, TestContext.Current.CancellationToken);

        var app = await database.RefusedToAppAsync($"DELETE FROM public.person_credential WHERE person_id = {id}");
        var owner = await database.RefusedToOwnerAsync($"DELETE FROM public.person_credential WHERE person_id = {id}");

        Assert.Equal(Npgsql.PostgresErrorCodes.InsufficientPrivilege, app.SqlState);
        Assert.Equal(Npgsql.PostgresErrorCodes.InsufficientPrivilege, owner.SqlState);
        Assert.NotNull(await database.CredentialOfAsync(id));
    }

    private async Task<long> NewPersonAsync() =>
        PeopleDatabase.Id(await (await database.RegistryAsync()).CreateAsync(new NewPerson(PeopleDatabase.NewName(), Annotation), CustodyAuthor.System, TestContext.Current.CancellationToken));
}
