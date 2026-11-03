using Core.Persistence.Connection;
using Core.Persistence.Content;
using Core.Persistence.Content.Sealing;
using Core.Persistence.Domains;
using Core.Persistence.Recovery;
using Core.Platform.Keys;
using Core.Platform.Startup;
using Npgsql;

namespace Core.Host.Loss;

/// <summary>
/// The mode asked for with <c>rehearse-recovery</c> (issue 67, decision 0014): run once in the clean environment, over the copy of the database and the copy of safekeeping of the key, it opens
/// in memory a sample of the raw text and of the reversal map of every domain and prints the receipt, which has no content in it. A rehearsal with a sample that did not open prints none.
/// </summary>
internal static class RecoveryRehearsalMode
{
    private const string Argument = "rehearse-recovery";

    internal static bool Requested(string[] args) => args is [Argument, ..];

    internal static async Task<HostLaunch> RunAsync(string[] args, TextWriter output, TextWriter error)
    {
        var loaded = HostConfiguration.Load(args[1..], migrating: true);
        var database = HostConfiguration.DatabaseOf(loaded, migrating: true);
        if (database is null || database.State == DatabaseSettingsState.Refused)
        {
            return HostLaunch.Refused(database?.Problems ?? loaded.Problems);
        }

        var missing = MasterKeyConfiguration.Missing(loaded.Values).ToList();
        if (missing.Count > 0)
        {
            await error.WriteLineAsync($"{Argument} recusado: falta {string.Join(", ", missing)}, que o ensaio exige").ConfigureAwait(false);
            return HostLaunch.Ended(ExitCode.ConfigurationRefused);
        }

        using var reading = await MasterKeyConfiguration.SourceOf(loaded.Values).ReadAsync(CancellationToken.None).ConfigureAwait(false);
        await using var dataSource = NpgsqlDataSource.Create(database.ConnectionString!);
        return HostLaunch.Ended(await RehearseAsync(dataSource, reading, new KeyLossMode.Words(output, error)).ConfigureAwait(false));
    }

    /// <summary>The rehearsal itself, over a source that was read: the key is not read again, and nothing of the content is shown.</summary>
    internal static async Task<ExitCode> RehearseAsync(NpgsqlDataSource dataSource, MasterKeyReading reading, KeyLossMode.Words words)
    {
        if (reading.Failure is not null)
        {
            await words.Error.WriteLineAsync($"{Argument} recusado: {reading.Failure.Found}").ConfigureAwait(false);
            return ExitCode.RecoveryRefused;
        }

        try
        {
            var registered = await RegisteredLabels.ReadAsync(dataSource, CancellationToken.None).ConfigureAwait(false);
            var content = new ProtectedContent(reading.Labels, CompositionRoot.KeysOf(reading), registered);
            var rehearsal = new RecoveryRehearsal(dataSource, content, TimeProvider.System);
            return await ReportAsync(await rehearsal.RehearseAsync(KeyProof.Of(reading.Key!), CancellationToken.None).ConfigureAwait(false), words).ConfigureAwait(false);
        }
        catch (InvalidOperationException defect)
        {
            await words.Error.WriteLineAsync($"{Argument} recusado: {defect.Message}").ConfigureAwait(false);
            return ExitCode.RecoveryRefused;
        }
        catch (NpgsqlException)
        {
            await words.Error.WriteLineAsync($"{Argument} falhou ao falar com o banco").ConfigureAwait(false);
            return ExitCode.RecoveryFailed;
        }
    }

    private static async Task<ExitCode> ReportAsync(RecoveryReceipt receipt, KeyLossMode.Words words)
    {
        var counts = receipt.Counts;
        if (!counts.Holds)
        {
            await words.Error.WriteLineAsync($"{Argument} recusado, sem recibo: abriram {counts.Raw} do bruto e {counts.Maps} do mapa, e {counts.Failed} não abriram").ConfigureAwait(false);
            return ExitCode.RecoveryRefused;
        }

        await words.Output.WriteLineAsync($"{Argument}: abriram {counts.Raw} do bruto e {counts.Maps} do mapa; recibo, para registrar na produção com record-recovery-test:").ConfigureAwait(false);
        await words.Output.WriteLineAsync(receipt.ToText()).ConfigureAwait(false);
        return ExitCode.Success;
    }
}
