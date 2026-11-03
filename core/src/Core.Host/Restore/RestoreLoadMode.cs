using System.Globalization;
using Core.Persistence.Connection;
using Core.Persistence.ServerGuarantees;
using Core.Persistence.ServerGuarantees.Restore;
using Core.Platform.Configuration;
using Core.Platform.Startup;
using Npgsql;

namespace Core.Host.Restore;

/// <summary>
/// The mode the operator asks for with <c>restore-load &lt;folder&gt; &lt;sequence&gt; &lt;digest&gt;</c> as the first arguments: the copy to put back and the tip noted outside it.
/// It runs once, with the application's credential, in a new database; the code of the exit is the verdict and the lines say what differs, never a value.
/// </summary>
internal static class RestoreLoadMode
{
    private const string Argument = "restore-load";
    private const int Positional = 4;

    private static readonly string[] KeyPaths = [MasterKeyConfiguration.FilePath, MasterKeyConfiguration.SecondFilePath, MasterKeyConfiguration.ThirdFilePath];

    internal static bool Requested(string[] args) => args is [Argument, ..];

    /// <summary>The arguments the configuration reads: without the mode word and the three that name the copy and the tip.</summary>
    internal static string[] ConfigurationArguments(string[] args) => Requested(args) ? args[Math.Min(Positional, args.Length)..] : args;

    internal static async Task<HostLaunch> LaunchAsync(string[] args, ConfigurationLoadResult loaded, DatabaseSettings database)
    {
        if (args.Length < Positional || !long.TryParse(args[2], NumberStyles.None, CultureInfo.InvariantCulture, out var sequence) || !KeyPaths.All(loaded.Values.ContainsKey))
        {
            await Console.Error.WriteLineAsync("restore-load pede a pasta da cópia, a sequência e o digest do topo anotado fora dela, e a chave mestra configurada.").ConfigureAwait(false);
            return HostLaunch.Loaded(ExitCode.ConfigurationRefused);
        }

        var result = await LoadAsync(loaded, database, args[1], new RestoreTip(sequence, args[3])).ConfigureAwait(false);
        await (result.Outcome == RestoreOutcome.Restored ? Console.Out : Console.Error).WriteLineAsync($"restauração: {result.Outcome} {result.Detail}".TrimEnd()).ConfigureAwait(false);
        return HostLaunch.Loaded(result.Outcome == RestoreOutcome.Restored ? ExitCode.Success : ExitCode.RestoreRefused);
    }

    private static async Task<RestoreResult> LoadAsync(ConfigurationLoadResult loaded, DatabaseSettings database, string folder, RestoreTip tip)
    {
        try
        {
            await using var source = NpgsqlDataSource.Create(database.ConnectionString!);
            var keys = MasterKeyConfiguration.SourceOf(loaded.Values);
            var verifier = new GuaranteeVerifier(source, keys.ReadAsync, reading => CompositionRoot.ContentReader(source, reading));
            return await new RestoreLoader(source, verifier, TimeProvider.System).LoadAsync(RestoreCopy.Open(folder), tip, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception failure) when (failure is NpgsqlException or IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return new RestoreResult(RestoreOutcome.CopyRefused, $"uma falha encerrou a carga ({failure.GetType().FullName})");
        }
    }
}
