using Core.Persistence.Migrations;
using Core.Persistence.People;
using Core.Persistence.Tests.Fixtures;
using Core.Persistence.Tests.ServerGuarantees;
using Npgsql;

namespace Core.Persistence.Tests.People;

/// <summary>
/// What the records of an operation that ends in an exception hold (#69, CA-4): a refusal of the server may carry the row that failed, which here is the salt and the result,
/// so each test makes the server refuse the write, with a constraint the owner adds to a database of its own, and reads the exception and everything the driver logged.
/// </summary>
public sealed class PersonCredentialsFailureLogTests
{
    private const string Password = "ZQX-SENHA-5914";
    private const int LowFactor = 2;
    private const int HighFactor = 5;

    private static readonly string Annotation = PersonFunctions.Accepted[0];

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task SettingThePasswordThatTheServerRefusesCarriesNoPasswordAndNoBytesInTheExceptionNorInTheLog()
    {
        await using var scratch = await MigratedAsync();
        var id = await NewPersonAsync(scratch);
        await scratch.RunAsOwnerAsync("ALTER TABLE public.person_credential ADD CONSTRAINT zqx_factor_floor CHECK (work_factor > 1000)");
        await using var logged = new LoggedCredentials(scratch.AppConnectionString, LowFactor);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => logged.Store.SetPasswordAsync(id, Password, actorId: null, TestContext.Current.CancellationToken));

        Assert.Contains(PostgresErrorCodes.CheckViolation, failure.Message, StringComparison.Ordinal);
        Assert.Contains("person_credential", logged.Log.Everything, StringComparison.Ordinal);
        AssertNothingIn($"{failure}\n{logged.Log.Everything}");
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task RecomputingThatTheServerRefusesCarriesNoPasswordNorEitherDerivationInTheExceptionNorInTheLog()
    {
        await using var scratch = await MigratedAsync();
        var id = await NewPersonAsync(scratch);
        var stored = await StoredAsync(scratch, id);
        await scratch.RunAsOwnerAsync("ALTER TABLE public.person_credential ADD CONSTRAINT zqx_factor_ceiling CHECK (work_factor < 3)");
        await using var logged = new LoggedCredentials(scratch.AppConnectionString, HighFactor);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => logged.Store.VerifyAsync(id, Password, TestContext.Current.CancellationToken));

        Assert.Contains(PostgresErrorCodes.CheckViolation, failure.Message, StringComparison.Ordinal);
        Assert.Contains("person_credential", logged.Log.Everything, StringComparison.Ordinal);
        AssertNothingIn($"{failure}\n{logged.Log.Everything}", stored);
    }

    [Fact]
    public async Task ConnectionThatFailsWhileSettingAndWhileVerifyingCarriesNeitherThePasswordNorTheServersPasswordInTheExceptionNorInTheLog()
    {
        await using var server = new UnreachableServer();
        await using var logged = new LoggedCredentials(server.ConnectionString, LowFactor);

        var whileSetting = await Assert.ThrowsAnyAsync<NpgsqlException>(() => logged.Store.SetPasswordAsync(1, Password, actorId: null, TestContext.Current.CancellationToken));
        var whileVerifying = await Assert.ThrowsAnyAsync<NpgsqlException>(() => logged.Store.VerifyAsync(1, Password, TestContext.Current.CancellationToken));

        Assert.NotEmpty(logged.Log.Everything);
        SecretEncodings.AssertNoneIn($"{whileSetting}\n{whileVerifying}\n{logged.Log.Everything}", SecretEncodings.Of(Password).Concat(SecretEncodings.Of(UnreachableServer.Password)));
        SecretEncodings.AssertNoByteaIn($"{whileSetting}\n{whileVerifying}\n{logged.Log.Everything}");
    }

    private static async Task<ScratchDatabase> MigratedAsync()
    {
        var scratch = await ScratchDatabase.CreateAsync();
        _ = await scratch.MigrateAsync(EmbeddedMigrations.Load());
        return scratch;
    }

    /// <summary>A person of the database, registered as the application does.</summary>
    private static async Task<long> NewPersonAsync(ScratchDatabase scratch)
    {
        await using var source = NpgsqlDataSource.Create(scratch.AppConnectionString);
        var created = await new PersonRegistry(source).CreateAsync(new NewPerson(PeopleDatabase.NewName(), Annotation), actorId: null, TestContext.Current.CancellationToken);
        return PeopleDatabase.Id(created);
    }

    /// <summary>Gives the person a password with the low factor and reads what was stored, before the owner adds the constraint that makes the server refuse the next write.</summary>
    private static async Task<PeopleDatabase.CredentialRow> StoredAsync(ScratchDatabase scratch, long id)
    {
        await using var source = NpgsqlDataSource.Create(scratch.AppConnectionString);
        _ = await new PersonCredentials(source, new PasswordDerivation(LowFactor)).SetPasswordAsync(id, Password, actorId: null, TestContext.Current.CancellationToken);
        await using var connection = await Sql.OpenAsync(scratch.OwnerConnectionString);
        await using var command = new NpgsqlCommand("SELECT algorithm, work_factor, salt, derived FROM public.person_credential WHERE person_id = @id", connection);
        _ = command.Parameters.AddWithValue("id", id);
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        _ = await reader.ReadAsync(TestContext.Current.CancellationToken);
        var salt = await reader.GetFieldValueAsync<byte[]>(2, TestContext.Current.CancellationToken);
        var derived = await reader.GetFieldValueAsync<byte[]>(3, TestContext.Current.CancellationToken);
        return new PeopleDatabase.CredentialRow(reader.GetString(0), reader.GetInt32(1), salt, derived);
    }

    private static void AssertNothingIn(string records, params PeopleDatabase.CredentialRow[] stored)
    {
        var secrets = SecretEncodings.Of(Password).Concat(stored.SelectMany(row => SecretEncodings.Of(row.Salt).Concat(SecretEncodings.Of(row.Derived))));

        SecretEncodings.AssertNoneIn(records, secrets);
        SecretEncodings.AssertNoByteaIn(records);
    }
}
