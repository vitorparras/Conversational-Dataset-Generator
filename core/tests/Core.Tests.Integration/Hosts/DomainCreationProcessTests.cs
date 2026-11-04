using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Core.Persistence.Migrations;
using Core.Persistence.Tests.Fixtures;
using Core.Tests.Support.Custody;
using Npgsql;

namespace Core.Tests.Integration.Hosts;

/// <summary>
/// The built host asked to create a domain (issue 54, CA-4), as its own process over a database of its own and with the owner credential: with the two labels in the custody file it writes
/// the domain, the labels and the event; with one missing it names it and exits with the code of the missing label; with the name taken it exits with the code of the repeated name.
/// Only the exit code, the words and the rows are looked at, never a key.
/// </summary>
[Trait("Category", "Database")]
public sealed class DomainCreationProcessTests
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(120);

    [DatabaseFact]
    public async Task Ca4aTheCommandCreatesTheDomainWithTheLabelsAndTheEventAndExitsWithZero()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        _ = await database.MigrateAsync(EmbeddedMigrations.Load());
        using var files = new CustodyFiles();

        var (exit, words) = await RunAsync(database, Arguments(files, "zqx-domain-a", "texto-bruto/1/v1", "mapa-reversao/1/v1"));

        Assert.Equal((0, true), (exit, words.Contains("domínio criado com o id 1", StringComparison.Ordinal)));
        Assert.Equal(
            (1L, 2L, 1L),
            (await ScalarAsync(database, "SELECT count(*) FROM public.domain"),
             await ScalarAsync(database, "SELECT count(*) FROM public.derivation_label"),
             await ScalarAsync(database, "SELECT count(*) FROM public.custody_event WHERE step = 'domain_created' AND item_kind = 'domain' AND item_id = 1")));
        Assert.DoesNotContain("zqx-domain-a", words, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task Ca4bTheCommandWithALabelMissingNamesItExitsWithItsCodeAndWritesNothing()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        _ = await database.MigrateAsync(EmbeddedMigrations.Load());
        using var files = new CustodyFiles();

        var (exit, words) = await RunAsync(database, Arguments(files, "zqx-domain-a", "texto-bruto/1/v1"));

        Assert.Equal((17, true, true), (exit, words.Contains("mapa-reversao/1/v1", StringComparison.Ordinal), words.Contains("id 1", StringComparison.Ordinal)));
        Assert.DoesNotContain("texto-bruto/1/v1", words, StringComparison.Ordinal);
        Assert.Equal((0L, 0L, 0L), await CountsAsync(database));
    }

    [DatabaseFact]
    public async Task Ca4cTheCommandWithTheNameOfADomainThatExistsExitsWithItsCodeAndWritesNothingMore()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        _ = await database.MigrateAsync(EmbeddedMigrations.Load());
        using var files = new CustodyFiles();
        var arguments = Arguments(files, "zqx-domain-a", "texto-bruto/1/v1", "mapa-reversao/1/v1", "texto-bruto/2/v1", "mapa-reversao/2/v1");

        var (first, _) = await RunAsync(database, arguments);
        var (second, _) = await RunAsync(database, arguments);

        Assert.Equal((0, 18), (first, second));
        Assert.Equal((1L, 2L, 1L), await CountsAsync(database));
    }

    [DatabaseFact]
    public async Task TheCommandWithoutTheNameRefusesWithTheConfigurationCodeBeforeAnythingElse()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        _ = await database.MigrateAsync(EmbeddedMigrations.Load());
        using var files = new CustodyFiles();
        var withoutName = Arguments(files, "zqx-domain-a", "texto-bruto/1/v1", "mapa-reversao/1/v1").Where(argument => !argument.StartsWith("--name", StringComparison.Ordinal)).ToArray();

        var (exit, words) = await RunAsync(database, withoutName);

        Assert.Equal((78, true), (exit, words.Contains("--name", StringComparison.Ordinal)));
        Assert.Equal((0L, 0L, 0L), await CountsAsync(database));
    }

    private static string[] Arguments(CustodyFiles files, string name, params string[] labels) =>
    [
        "create-domain",
        $"--name={name}",
        $"--MasterKey:File={files.Write("key", System.Text.Encoding.ASCII.GetBytes(Convert.ToBase64String(CustodyFiles.SyntheticKey()) + "\n"))}",
        $"--MasterKey:LabelsFile={files.WriteText("labels", string.Concat(labels.Select(label => label + "\n")))}",
        $"--MasterKey:EpochsFile={files.WriteText("epochs", $"1:{Convert.ToBase64String(CustodyFiles.SyntheticEpoch(1))}\n")}",
    ];

    private static async Task<(long, long, long)> CountsAsync(ScratchDatabase database) =>
        (await ScalarAsync(database, "SELECT count(*) FROM public.domain"),
         await ScalarAsync(database, "SELECT count(*) FROM public.derivation_label"),
         await ScalarAsync(database, "SELECT count(*) FROM public.custody_event"));

    [SuppressMessage("Security", "CA2100", Justification = "The statements are literals of the tests, never input.")]
    private static async Task<long> ScalarAsync(ScratchDatabase database, string statement)
    {
        await using var connection = await Sql.OpenAsync(database.OwnerConnectionString);
        await using var command = new NpgsqlCommand(statement, connection);
        return (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    private static async Task<(int ExitCode, string Output)> RunAsync(ScratchDatabase database, string[] arguments)
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
