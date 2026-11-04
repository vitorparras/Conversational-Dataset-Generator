using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Core.Persistence.Domains;
using Core.Persistence.Migrations;
using Core.Persistence.Recovery;
using Core.Persistence.Tests.Fixtures;
using Core.Platform.Keys;
using Core.Tests.Support.Custody;
using Npgsql;

namespace Core.Tests.Integration.Hosts;

/// <summary>
/// The built host asked to record the rehearsal of the recovery (issue 67, CA-3), as its own process over a production of its own and the key of the custody files:
/// it writes the step when the top of the
/// receipt is in the chain and the key is the one of the files, and otherwise exits with the refusal code, says which of the two failed and writes nothing.
/// </summary>
[Trait("Category", "Database")]
public sealed class RecoveryRecordProcessTests
{
    private const string Tip = "SELECT sequence || ',' || encode(digest, 'hex') FROM public.custody_event ORDER BY sequence DESC LIMIT 1";
    private const string Written = "SELECT count(*) FROM public.custody_event WHERE step = 'recovery_tested'";
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(120);
    private static readonly string[] Labels = ["texto-bruto/1/v1", "mapa-reversao/1/v1"];

    [DatabaseFact]
    public async Task Ca3TheCommandWritesTheStepWhenTheTopIsInTheChainAndTheKeyIsTheOneOfTheFilesAndTheGateSaysItIsTested()
    {
        await using var production = await ProductionAsync();
        using var files = new CustodyFiles();
        var receipt = await ReceiptAsync(production, ProofOf(CustodyFiles.SyntheticKey()));

        var (exit, words) = await RunAsync(production, Arguments(files, receipt.ToText()));

        Assert.Equal((0, 1L), (exit, await ScalarAsync(production, Written)));
        Assert.Contains("ensaio registrado", words, StringComparison.Ordinal);
        await using var source = NpgsqlDataSource.Create(production.AppConnectionString);
        Assert.True((await RecoveryGate.StatusAsync(source, TestContext.Current.CancellationToken)).Tested);
    }

    [DatabaseFact]
    public async Task Ca3TheCommandWithAKeyThatIsNotTheOneOfTheFilesRefusesNamingTheKeyAndWritesNothing()
    {
        await using var production = await ProductionAsync();
        using var files = new CustodyFiles();
        var receipt = await ReceiptAsync(production, ProofOf([.. Enumerable.Range(0, MasterKey.Length).Select(index => (byte)(index + 1))]));

        var (exit, words) = await RunAsync(production, Arguments(files, receipt.ToText()));

        Assert.Equal(
            (29, true, false, 0L),
            (exit, words.Contains("a chave do ensaio", StringComparison.Ordinal), words.Contains("o topo da cópia", StringComparison.Ordinal), await ScalarAsync(production, Written)));
    }

    [DatabaseFact]
    public async Task Ca3TheCommandWithATopThatIsNotInTheChainRefusesNamingTheTopAndWritesNothing()
    {
        await using var production = await ProductionAsync();
        using var files = new CustodyFiles();
        var receipt = (await ReceiptAsync(production, ProofOf(CustodyFiles.SyntheticKey()))) with { TopDigest = new string('0', 64) };

        var (exit, words) = await RunAsync(production, Arguments(files, receipt.ToText()));

        Assert.Equal(
            (29, false, true, 0L),
            (exit, words.Contains("a chave do ensaio", StringComparison.Ordinal), words.Contains("o topo da cópia", StringComparison.Ordinal), await ScalarAsync(production, Written)));
    }

    [DatabaseFact]
    public async Task Ca2TheCommandWithAReceiptCutShortRefusesWithTheReasonAndWritesNothing()
    {
        await using var production = await ProductionAsync();
        using var files = new CustodyFiles();
        var receipt = await ReceiptAsync(production, ProofOf(CustodyFiles.SyntheticKey()));

        var (exit, words) = await RunAsync(production, Arguments(files, receipt.ToText()[..40]));

        Assert.Equal((29, true, 0L), (exit, words.Contains("recusado, nada foi gravado", StringComparison.Ordinal), await ScalarAsync(production, Written)));
    }

    [DatabaseFact]
    public async Task TheCommandWithoutTheReceiptRefusesWithTheConfigurationCode()
    {
        await using var production = await ProductionAsync();
        using var files = new CustodyFiles();

        var (exit, words) = await RunAsync(production, Arguments(files, null));

        Assert.Equal((78, true), (exit, words.Contains("--receipt", StringComparison.Ordinal)));
    }

    private static string ProofOf(byte[] key)
    {
        using var reading = MasterKeyReading.FromBytes(key, DerivationLabels.None, "teste");
        return KeyProof.Of(reading.Key!);
    }

    private static string[] Arguments(CustodyFiles files, string? receipt)
    {
        var withKey = new[]
        {
            "record-recovery-test",
            $"--MasterKey:File={files.Write("key", Encoding.ASCII.GetBytes(Convert.ToBase64String(CustodyFiles.SyntheticKey()) + "\n"))}",
            $"--MasterKey:LabelsFile={files.WriteText("labels", string.Concat(Labels.Select(label => label + "\n")))}",
            $"--MasterKey:EpochsFile={files.WriteText("epochs", CustodyFiles.EpochLine)}",
        };
        return receipt is null ? withKey : [.. withKey, $"--receipt={receipt}"];
    }

    private static async Task<RecoveryReceipt> ReceiptAsync(ScratchDatabase production, string proof)
    {
        var top = (await TextAsync(production, Tip)).Split(',');
        var at = new DateTimeOffset(2031, 3, 4, 8, 6, 7, TimeSpan.Zero);
        return new RecoveryReceipt(at, long.Parse(top[0], System.Globalization.CultureInfo.InvariantCulture), top[1], proof, new SampleCounts(1, 1, 0));
    }

    /// <summary>A migrated database with a domain, so that its chain has an event.</summary>
    private static async Task<ScratchDatabase> ProductionAsync()
    {
        var database = await ScratchDatabase.CreateAsync();
        _ = await database.MigrateAsync(EmbeddedMigrations.Load());
        await using var owner = NpgsqlDataSource.Create(database.OwnerConnectionString);
        _ = await new DomainCreation(owner).CreateAsync("zqx-domain-a", DerivationLabels.Of(Labels), TestContext.Current.CancellationToken);
        return database;
    }

    [SuppressMessage("Security", "CA2100", Justification = "The statements are literals of the tests, never input.")]
    private static async Task<string> TextAsync(ScratchDatabase database, string statement)
    {
        await using var connection = await Sql.OpenAsync(database.OwnerConnectionString);
        await using var command = new NpgsqlCommand(statement, connection);
        return (string)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

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
