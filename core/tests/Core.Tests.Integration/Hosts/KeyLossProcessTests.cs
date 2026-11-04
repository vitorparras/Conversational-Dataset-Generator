using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Core.Persistence.Custody.Verification;
using Core.Persistence.Migrations;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Tests.Integration.Hosts;

/// <summary>
/// The built host asked to declare the loss of a key (issue 67, CA-2), as its own process over a database of its own: it writes the step key_loss_declared with the scope and what was tried, the
/// chain verifies, and a scope or an attempt outside the fixed lists is refused with the code of the refusal and nothing written. It needs no key file.
/// </summary>
[Trait("Category", "Database")]
public sealed class KeyLossProcessTests
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(120);

    [DatabaseTheory]
    [InlineData("master", "copia-de-guarda,copia-anterior-da-guarda", 3L)]
    [InlineData("texto-bruto/1/v1", "registro-de-criacao-do-dominio", 4L)]
    public async Task Ca2TheCommandWritesTheStepWithTheScopeAndWhatWasTriedAndTheChainVerifies(string scope, string tried, long sum)
    {
        await using var database = await MigratedAsync();

        var (exit, words) = await RunAsync(database, "declare-key-loss", $"--scope={scope}", $"--tried={tried}");

        Assert.Equal((0, true), (exit, words.Contains(scope, StringComparison.Ordinal)));
        const string Written = "SELECT count(*) FROM public.custody_event WHERE step = 'key_loss_declared' AND item_kind = 'master_key' AND actor_system AND occurred_at IS NOT NULL";
        Assert.Equal(1L, await ScalarAsync(database, $"{Written} AND item_id = {sum} AND reference = '{scope}'"));
        await using var source = NpgsqlDataSource.Create(database.AppConnectionString);
        Assert.True((await new CustodyVerifier(source, TimeProvider.System).VerifyAsync(TestContext.Current.CancellationToken)).Passed);
    }

    [DatabaseTheory]
    [InlineData("segredo", "copia-de-guarda")]
    [InlineData("master", "zqx-livre")]
    [InlineData("master", "copia-de-guarda,copia-de-guarda")]
    public async Task Ca2AScopeOrAnAttemptOutsideTheListsIsRefusedWithItsCodeAndNothingIsWritten(string scope, string tried)
    {
        await using var database = await MigratedAsync();

        var (exit, _) = await RunAsync(database, "declare-key-loss", $"--scope={scope}", $"--tried={tried}");

        Assert.Equal((27, 0L), (exit, await ScalarAsync(database, "SELECT count(*) FROM public.custody_event WHERE step = 'key_loss_declared'")));
    }

    [DatabaseFact]
    public async Task TheCommandWithoutItsOptionsRefusesWithTheConfigurationCodeAndWritesNothing()
    {
        await using var database = await MigratedAsync();

        var (exit, words) = await RunAsync(database, "declare-key-loss", "--scope=master");

        Assert.Equal((78, true, 0L), (exit, words.Contains("--tried", StringComparison.Ordinal), await ScalarAsync(database, "SELECT count(*) FROM public.custody_event")));
    }

    private static async Task<ScratchDatabase> MigratedAsync()
    {
        var database = await ScratchDatabase.CreateAsync();
        _ = await database.MigrateAsync(EmbeddedMigrations.Load());
        return database;
    }

    [SuppressMessage("Security", "CA2100", Justification = "The statements are literals of the tests, never input.")]
    private static async Task<long> ScalarAsync(ScratchDatabase database, string statement)
    {
        await using var connection = await Sql.OpenAsync(database.OwnerConnectionString);
        await using var command = new NpgsqlCommand(statement, connection);
        return (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    private static async Task<(int ExitCode, string Output)> RunAsync(ScratchDatabase database, params string[] arguments)
    {
        var owner = new NpgsqlConnectionStringBuilder(database.OwnerConnectionString);
        var info = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, OperatingSystem.IsWindows() ? "Core.Host.exe" : "Core.Host"))
        {
            WorkingDirectory = AppContext.BaseDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        _ = info.Environment.Remove("STRYKER_COVERAGE_FILE");
        _ = info.Environment.Remove("STRYKER_COVERAGE_EPOCH_FILE");
        info.Environment["DOTNET_ENVIRONMENT"] = "Development";
        info.Environment["CDG_Database__Host"] = $"{owner.Host}:{owner.Port}";
        info.Environment["CDG_Database__Name"] = owner.Database;
        info.Environment["CDG_Database__Username"] = owner.Username;
        info.Environment["CDG_Database__Password"] = owner.Password;
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        using var process = Process.Start(info) ?? throw new InvalidOperationException("The host process did not start.");
        var output = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var error = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(Limit);
        await process.WaitForExitAsync(timeout.Token);
        return (process.ExitCode, await output + await error);
    }
}
