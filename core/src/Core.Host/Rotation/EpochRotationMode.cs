using Core.Persistence.Connection;
using Core.Persistence.Content;
using Core.Platform.Keys;
using Core.Platform.Startup;
using Npgsql;

namespace Core.Host.Rotation;

/// <summary>
/// The mode the operator asks for with <c>rotate-epoch</c> as the first argument (decision 0049): the same executable, run once with the owner credential as the migration is,
/// wraps again under the newest epoch of the epochs file every key an older one wraps. It serves nothing, and says what it did without a key or an epoch in the words.
/// </summary>
internal static class EpochRotationMode
{
    private const string Argument = "rotate-epoch";

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
            await error.WriteLineAsync($"rotate-epoch recusado: falta {string.Join(", ", missing)}, que o giro exige").ConfigureAwait(false);
            return HostLaunch.Ended(ExitCode.ConfigurationRefused);
        }

        using var reading = await MasterKeyConfiguration.SourceOf(loaded.Values).ReadAsync(CancellationToken.None).ConfigureAwait(false);
        await using var dataSource = NpgsqlDataSource.Create(database.ConnectionString!);
        return HostLaunch.Ended(await RotateAsync(dataSource, reading, output, error).ConfigureAwait(false));
    }

    /// <summary>The rotation itself, over a source that was read: the key and the epochs are not read again, and nothing of them is written.</summary>
    internal static async Task<ExitCode> RotateAsync(NpgsqlDataSource dataSource, MasterKeyReading reading, TextWriter output, TextWriter error)
    {
        if (reading.Failure is not null)
        {
            await error.WriteLineAsync($"rotate-epoch recusado: {reading.Failure.Found}").ConfigureAwait(false);
            return ExitCode.EpochRotationRefused;
        }

        try
        {
            var keys = new ItemKeys(new KeyDerivation(reading.Key!, reading.Labels), reading.Epochs);
            var rewrapped = await new EpochRotation(dataSource, keys).RotateAsync(CancellationToken.None).ConfigureAwait(false);
            await output.WriteLineAsync($"rotate-epoch: {rewrapped} chave(s) reembrulhada(s) para a época {keys.Newest}").ConfigureAwait(false);
            return ExitCode.Success;
        }
        catch (EnvelopeRefusedException refusal)
        {
            await error.WriteLineAsync($"rotate-epoch recusado, nada foi alterado: {refusal.Message}").ConfigureAwait(false);
            return ExitCode.EpochRotationRefused;
        }
        catch (NpgsqlException)
        {
            await error.WriteLineAsync("rotate-epoch falhou ao falar com o banco, nada foi alterado").ConfigureAwait(false);
            return ExitCode.EpochRotationFailed;
        }
    }
}
