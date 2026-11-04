using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Core.Persistence.Migrations;
using Core.Persistence.Tests.Fixtures;
using Core.Platform.Keys;
using Core.Tests.Support.Custody;
using Npgsql;

namespace Core.Tests.Integration.Hosts;

/// <summary>
/// The built host asked to rotate the epoch (decision 0049), as its own process over a database of its own: the key of a row that the epoch 1 wraps is wrapped under the epoch 2 and the
/// chain holds the one event of the rotation; a second run has nothing older and writes no event. Only the exit code, the count and the rows are looked at, never a key.
/// </summary>
[Trait("Category", "Database")]
public sealed class EpochRotationProcessTests
{
    private const string Label = "texto-bruto/1/v1";
    private const string Rotated = "SELECT count(*) FROM public.custody_event WHERE step = 'epoch_rotated' AND item_kind = 'content_epoch' AND item_id = 2";
    private const string EpochOfTheRow = "SELECT set_config('app.domain', '1', false); SELECT dek_epoch FROM content.item_text";
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(120);

    [DatabaseFact]
    public async Task RotateEpochWrapsTheKeysAgainUnderTheNewestEpochAndRecordsTheEventOnce()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        _ = await database.MigrateAsync(EmbeddedMigrations.Load());
        await database.RunAsOwnerAsync("INSERT INTO public.domain (name) VALUES ('zqx-domain-a')");
        await AddRowWrappedUnderTheEpochOneAsync(database);
        using var files = new CustodyFiles();
        var arguments = Arguments(files, [1, 2]);

        var (firstExit, firstOutput) = await RunAsync(database, arguments);
        var (secondExit, secondOutput) = await RunAsync(database, arguments);

        Assert.Equal((0, true), (firstExit, firstOutput.Contains("1 chave(s) reembrulhada(s) para a época 2", StringComparison.Ordinal)));
        Assert.Equal((0, true), (secondExit, secondOutput.Contains("0 chave(s) reembrulhada(s) para a época 2", StringComparison.Ordinal)));
        Assert.Equal((2, 1L), (await EpochAsync(database), await ScalarAsync(database, Rotated)));
    }

    [DatabaseFact]
    public async Task RotateEpochWithTheEpochsFileLackingTheEpochOfARowRefusesAndChangesNothing()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        _ = await database.MigrateAsync(EmbeddedMigrations.Load());
        await database.RunAsOwnerAsync("INSERT INTO public.domain (name) VALUES ('zqx-domain-a')");
        await AddRowWrappedUnderTheEpochOneAsync(database);
        using var files = new CustodyFiles();
        var onlyTheNewest = Arguments(files, [2]);

        var (exit, _) = await RunAsync(database, onlyTheNewest);

        Assert.Equal((15, 1, 0L), (exit, await EpochAsync(database), await ScalarAsync(database, Rotated)));
    }

    private static string[] Arguments(CustodyFiles files, IEnumerable<int> epochs) =>
    [
        "rotate-epoch",
        $"--MasterKey:File={files.Write("key", System.Text.Encoding.ASCII.GetBytes(Convert.ToBase64String(CustodyFiles.SyntheticKey()) + "\n"))}",
        $"--MasterKey:LabelsFile={files.WriteText("labels", Label + "\n")}",
        $"--MasterKey:EpochsFile={files.WriteText("epochs", string.Concat(epochs.Select(EpochLine)))}",
    ];

    private static string EpochLine(int epoch) => $"{epoch}:{Convert.ToBase64String(CustodyFiles.SyntheticEpoch(epoch))}\n";

    private static async Task AddRowWrappedUnderTheEpochOneAsync(ScratchDatabase database)
    {
        using var reading = MasterKeyReading.FromBytes(
            CustodyFiles.SyntheticKey(), DerivationLabels.Of(Enumerable.Repeat(Label, 1)), CustodyEpochs.Of(new Dictionary<int, byte[]> { [1] = CustodyFiles.SyntheticEpoch(1) }), "test");
        var keys = new ItemKeys(new KeyDerivation(reading.Key!, reading.Labels), reading.Epochs);
        var wrapped = keys.Create(Label);
        await using var connection = await Sql.OpenAsync(database.OwnerConnectionString);
        await using var command = new NpgsqlCommand(
            "SELECT set_config('app.domain', '1', false); "
            + "INSERT INTO content.item_text (domain_id, raw_text, dek_epoch, dek_wrapped) VALUES (1, '\\x01', @epoch, @wrapped)",
            connection);
        _ = command.Parameters.AddWithValue("epoch", wrapped.Epoch);
        _ = command.Parameters.AddWithValue("wrapped", wrapped.Wrapped.ToArray());
        _ = await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        wrapped.Key.Dispose();
    }

    private static async Task<int> EpochAsync(ScratchDatabase database)
    {
        await using var connection = await Sql.OpenAsync(database.OwnerConnectionString);
        await using var command = new NpgsqlCommand(EpochOfTheRow, connection);
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        _ = await reader.NextResultAsync(TestContext.Current.CancellationToken);
        _ = await reader.ReadAsync(TestContext.Current.CancellationToken);
        return reader.GetInt32(0);
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
