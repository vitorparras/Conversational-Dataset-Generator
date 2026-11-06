using Core.Host.Domains;
using Core.Persistence.Connection;
using Core.Persistence.Recovery;
using Core.Platform.Keys;
using Core.Platform.Startup;
using Npgsql;

namespace Core.Host.Loss;

/// <summary>
/// The mode asked for with <c>record-recovery-test --receipt &lt;text&gt;</c> (issue 67, decision 0014): run once in the production with the owner credential,
/// it writes the step of the rehearsal only if the top of the copy is in this chain and the key of the receipt is the one of the custody file here,
/// and otherwise says which of the two failed.
/// </summary>
internal static class RecoveryRecordMode
{
    private const string Argument = "record-recovery-test";
    private const string ReceiptOption = "--receipt";

    internal static bool Requested(string[] args) => args is [Argument, ..];

    internal static async Task<HostLaunch> RunAsync(string[] args, TextWriter output, TextWriter error)
    {
        var (receipt, rest) = DomainCreationMode.Split(args, ReceiptOption);
        if (receipt is null)
        {
            await error.WriteLineAsync($"{Argument} recusado: informe {ReceiptOption} uma vez, com o texto do recibo").ConfigureAwait(false);
            return HostLaunch.Ended(ExitCode.ConfigurationRefused);
        }

        var loaded = HostConfiguration.Load(rest, migrating: true);
        var database = HostConfiguration.DatabaseOf(loaded);
        if (database is null || database.State == DatabaseSettingsState.Refused)
        {
            return HostLaunch.Refused(database?.Problems ?? loaded.Problems);
        }

        var missing = MasterKeyConfiguration.Missing(loaded.Values).ToList();
        if (missing.Count > 0)
        {
            await error.WriteLineAsync($"{Argument} recusado: falta {string.Join(", ", missing)}, que o registro exige").ConfigureAwait(false);
            return HostLaunch.Ended(ExitCode.ConfigurationRefused);
        }

        using var reading = await MasterKeyConfiguration.SourceOf(loaded.Values).ReadAsync(CancellationToken.None).ConfigureAwait(false);
        await using var dataSource = NpgsqlDataSource.Create(database.ConnectionString!);
        return HostLaunch.Ended(await RecordAsync(dataSource, reading, receipt, new KeyLossMode.Words(output, error)).ConfigureAwait(false));
    }

    /// <summary>The record itself, over a source that was read: the key is not read again, and only its proof is compared.</summary>
    internal static async Task<ExitCode> RecordAsync(NpgsqlDataSource dataSource, MasterKeyReading reading, string receipt, KeyLossMode.Words words)
    {
        if (reading.Failure is not null)
        {
            await words.Error.WriteLineAsync($"{Argument} recusado, nada foi gravado: {reading.Failure.Found}").ConfigureAwait(false);
            return ExitCode.RecoveryRefused;
        }

        try
        {
            var record = await CompositionRoot.RecordingOf(dataSource).RecordAsync(receipt, KeyProof.Of(reading.Key!), CancellationToken.None).ConfigureAwait(false);
            return await ReportAsync(record, words).ConfigureAwait(false);
        }
        catch (NpgsqlException)
        {
            await words.Error.WriteLineAsync($"{Argument} falhou ao falar com o banco, nada foi gravado").ConfigureAwait(false);
            return ExitCode.RecoveryFailed;
        }
    }

    private static async Task<ExitCode> ReportAsync(RecoveryRecord record, KeyLossMode.Words words)
    {
        if (record.Outcome == RecoveryOutcome.Recorded)
        {
            await words.Output.WriteLineAsync($"{Argument}: ensaio registrado na cadeia com a sequência {record.Sequence}").ConfigureAwait(false);
            return ExitCode.Success;
        }

        await words.Error.WriteLineAsync($"{Argument} recusado, nada foi gravado: {string.Join("; ", record.Failures)}").ConfigureAwait(false);
        return ExitCode.RecoveryRefused;
    }
}
