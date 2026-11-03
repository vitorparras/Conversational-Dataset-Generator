using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Core.Persistence.Custody;
using Core.Persistence.Custody.Chain;
using Core.Persistence.Custody.Verification;
using Core.Persistence.Migrations;
using Core.Persistence.People;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Tests.Integration.Hosts;

/// <summary>
/// The built host asked to create the first person (issue 65), as its own process over a database of its own, with the application's credential and the password on the standard input:
/// with the registry empty it makes the person, the two events of the chain and a password that opens; with someone there it refuses with its own code and writes nothing; with a field not
/// accepted it names the field; with the owner's credential it refuses; and what the person did stays theirs after they are deactivated. Only the exit code, the words and the rows are looked at.
/// </summary>
[Trait("Category", "Database")]
public sealed class PersonCreationProcessTests
{
    private const string Password = "ZQX-first-password-5521";
    private const string Name = "zqx-first-person";
    private const string Function = "Administração técnica";
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(120);

    [DatabaseFact]
    public async Task Ca1TheCommandOnAnEmptyRegistryMakesThePersonWithTheTwoEventsAndAPasswordThatOpens()
    {
        await using var database = await MigratedAsync();

        var (exit, words) = await RunAsync(database.AppConnectionString, Arguments(Name, Function), Password);

        await using var source = NpgsqlDataSource.Create(database.AppConnectionString);
        var id = (long)(await ScalarAsync(database, "SELECT id FROM public.person"))!;
        var credentials = new PersonCredentials(source, new PasswordDerivation(1));
        Assert.Equal((0, true), (exit, words.Contains($"id {id}", StringComparison.Ordinal)));
        Assert.Equal(
            (1L, 1L, 1L),
            (await CountAsync(database, "public.person WHERE active AND function_name = 'Administração técnica'"),
             await CountAsync(database, $"public.custody_event WHERE step = 'person_created' AND actor_system AND item_kind = 'person' AND item_id = {id}"),
             await CountAsync(database, $"public.custody_event WHERE step = 'person_password_set' AND actor_system AND item_kind = 'person' AND item_id = {id}")));
        Assert.Equal(
            (PasswordOutcome.Accepted, PasswordOutcome.Wrong),
            (await credentials.VerifyAsync(id, Password, TestContext.Current.CancellationToken), await credentials.VerifyAsync(id, Password + "x", TestContext.Current.CancellationToken)));
    }

    [DatabaseFact]
    public async Task Ca2TheCommandOnARegistryWithSomeoneRefusesWithItsCodeWritesNothingAndPointsAtTheRoute()
    {
        await using var database = await MigratedAsync();
        _ = await RunAsync(database.AppConnectionString, Arguments(Name, Function), Password);
        var before = await CountAsync(database, "public.custody_event");

        var (exit, words) = await RunAsync(database.AppConnectionString, Arguments("zqx-second-person", Function), Password);

        Assert.Equal((21, true), (exit, words.Contains("rota com sessão", StringComparison.Ordinal)));
        Assert.Equal((1L, before), (await CountAsync(database, "public.person"), await CountAsync(database, "public.custody_event")));
    }

    [DatabaseTheory]
    [InlineData("zqx-first-person", "Cozinheiro", Password, "função")]
    [InlineData("", Function, Password, "nome")]
    [InlineData("zqx-first-person", Function, "", "senha")]
    public async Task Ca3AFieldNotAcceptedRefusesWritingNothingAndNamingTheField(string name, string function, string password, string field)
    {
        await using var database = await MigratedAsync();

        var (exit, words) = await RunAsync(database.AppConnectionString, Arguments(name, function), password);

        Assert.Equal((22, true), (exit, words.Contains($"o campo '{field}'", StringComparison.Ordinal)));
        Assert.Equal((0L, 0L), (await CountAsync(database, "public.person"), await CountAsync(database, "public.custody_event")));
    }

    [DatabaseFact]
    public async Task Ca4ThePasswordIsNeverInTheWordsAndAPasswordAsAnArgumentIsRefusedWithoutBeingRepeated()
    {
        await using var database = await MigratedAsync();

        var (made, madeWords) = await RunAsync(database.AppConnectionString, Arguments(Name, Function), Password);
        var (refused, refusedWords) = await RunAsync(database.AppConnectionString, [.. Arguments("zqx-other", Function), $"--password={Password}"], Password);

        Assert.Equal((0, 78), (made, refused));
        Assert.All([madeWords, refusedWords], words => Assert.DoesNotContain(Password, words, StringComparison.Ordinal));
        Assert.Contains("entrada padrão", refusedWords, StringComparison.Ordinal);
        Assert.Equal(1L, await CountAsync(database, "public.person"));
    }

    [DatabaseFact]
    public async Task Ca5TheCommandRunsOnlyWithTheApplicationCredentialAndRefusesTheOwnersWritingNothing()
    {
        await using var database = await MigratedAsync();

        var (exit, words) = await RunAsync(database.OwnerConnectionString, Arguments(Name, Function), Password);

        Assert.Equal((22, true), (exit, words.Contains("credencial é a de dono", StringComparison.Ordinal)));
        Assert.Equal((0L, 0L), (await CountAsync(database, "public.person"), await CountAsync(database, "public.custody_event")));
    }

    [DatabaseFact]
    public async Task Ca6WhatThePersonDidStaysTheirsAfterTheyAreDeactivatedAndTheChainStillVerifies()
    {
        await using var database = await MigratedAsync();
        _ = await RunAsync(database.AppConnectionString, Arguments(Name, Function), Password);
        var id = (long)(await ScalarAsync(database, "SELECT id FROM public.person"))!;
        await using var source = NpgsqlDataSource.Create(database.AppConnectionString);
        var done = CustodyEvent.Of(CustodyAuthor.Person(id), new CustodyItem(CustodyItemKind.ItemText, 7), CustodyStep.RawTextRead, CustodyReason.ContentRead);
        _ = await new CustodyChain(source).AppendAsync(done, TestContext.Current.CancellationToken);
        const string Query = "SELECT actor_person_id FROM public.custody_event WHERE step = 'raw_text_read' AND item_id = 7";

        var whileActive = await ScalarAsync(database, Query);
        var deactivated = await new PersonRegistry(source).DeactivateAsync(id, CustodyAuthor.System, TestContext.Current.CancellationToken);
        var afterwards = await ScalarAsync(database, Query);
        var verification = await new CustodyVerifier(source, TimeProvider.System).VerifyAsync(TestContext.Current.CancellationToken);

        Assert.Equal((id, id, PersonOutcome.Done, true), ((long)whileActive!, (long)afterwards!, deactivated.Outcome, verification.Passed));
        Assert.Equal(0L, await CountAsync(database, "public.person WHERE active"));
    }

    [DatabaseFact]
    public async Task Ca7APasswordTheServerRefusesAfterThePersonWasMadeLeavesThePersonWithoutPasswordNamesTheIdAndNeverPrintsThePassword()
    {
        await using var database = await MigratedAsync();
        _ = await ScalarAsync(
            database,
            """
            CREATE FUNCTION public.zqx_refuse_credential() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'zqx refused'; END $$;
            CREATE TRIGGER zqx_refuse BEFORE INSERT ON public.person_credential FOR EACH ROW EXECUTE FUNCTION public.zqx_refuse_credential();
            """);

        var (exit, words) = await RunAsync(database.AppConnectionString, Arguments(Name, Function), Password);

        var id = (long)(await ScalarAsync(database, "SELECT id FROM public.person"))!;
        Assert.Equal((22, true), (exit, words.Contains($"a pessoa foi criada com o id {id} e a senha não foi definida", StringComparison.Ordinal)));
        Assert.Equal((1L, 0L), (await CountAsync(database, "public.person"), await CountAsync(database, "public.person_credential")));
        Assert.All([Password, Name], secret => Assert.DoesNotContain(secret, words, StringComparison.Ordinal));
    }

    [DatabaseFact]
    public async Task Ca8APasswordThatTheRegistryDoesNotStoreAfterThePersonWasMadeFailsWithItsCodeNamingTheId()
    {
        await using var database = await MigratedAsync();
        _ = await ScalarAsync(
            database,
            """
            CREATE FUNCTION public.zqx_skip_credential() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;
            CREATE TRIGGER zqx_skip BEFORE INSERT ON public.person_credential FOR EACH ROW EXECUTE FUNCTION public.zqx_skip_credential();
            """);

        var (exit, words) = await RunAsync(database.AppConnectionString, Arguments(Name, Function), Password);

        var id = (long)(await ScalarAsync(database, "SELECT id FROM public.person"))!;
        Assert.Equal((23, true), (exit, words.Contains($"a pessoa foi criada com o id {id} e a senha não foi definida", StringComparison.Ordinal)));
        Assert.Equal(0L, await CountAsync(database, "public.person_credential"));
        Assert.All([Password, Name], secret => Assert.DoesNotContain(secret, words, StringComparison.Ordinal));
    }

    private static string[] Arguments(string name, string function) =>
    [
        "create-person",
        "--name",
        name,
        "--function",
        function,
        "--Installation:Kind=demonstracao",
        "--Passwords:WorkFactor=1",
    ];

    private static async Task<ScratchDatabase> MigratedAsync()
    {
        var database = await ScratchDatabase.CreateAsync();
        _ = await database.MigrateAsync(EmbeddedMigrations.Load());
        return database;
    }

    [SuppressMessage("Security", "CA2100", Justification = "The statements are literals of the tests, never input.")]
    private static async Task<object?> ScalarAsync(ScratchDatabase database, string statement)
    {
        await using var connection = await Sql.OpenAsync(database.OwnerConnectionString);
        await using var command = new NpgsqlCommand(statement, connection);
        return await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<long> CountAsync(ScratchDatabase database, string from) =>
        (long)(await ScalarAsync(database, $"SELECT count(*) FROM {from}"))!;

    private static async Task<(int ExitCode, string Output)> RunAsync(string connectionString, string[] arguments, string password)
    {
        var credential = new NpgsqlConnectionStringBuilder(connectionString);
        var info = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, OperatingSystem.IsWindows() ? "Core.Host.exe" : "Core.Host"))
        {
            WorkingDirectory = AppContext.BaseDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        _ = info.Environment.Remove("STRYKER_COVERAGE_FILE");
        _ = info.Environment.Remove("STRYKER_COVERAGE_EPOCH_FILE");
        info.Environment["DOTNET_ENVIRONMENT"] = "Development";
        info.Environment["CDG_Database__Host"] = $"{credential.Host}:{credential.Port}";
        info.Environment["CDG_Database__Name"] = credential.Database;
        info.Environment["CDG_Database__Username"] = credential.Username;
        info.Environment["CDG_Database__Password"] = credential.Password;
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        using var process = Process.Start(info) ?? throw new InvalidOperationException("The host process did not start.");
        await process.StandardInput.WriteLineAsync(password.AsMemory(), TestContext.Current.CancellationToken);
        process.StandardInput.Close();
        var output = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var error = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(Limit);
        await process.WaitForExitAsync(timeout.Token);
        return (process.ExitCode, await output + await error);
    }
}
