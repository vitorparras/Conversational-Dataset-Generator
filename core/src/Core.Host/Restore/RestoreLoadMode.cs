using System.Globalization;
using Core.Persistence.Connection;
using Core.Persistence.ServerGuarantees;
using Core.Persistence.ServerGuarantees.Restore;
using Core.Platform.Startup;
using Npgsql;

namespace Core.Host.Restore;

/// <summary>The mode of <c>restore-load &lt;folder&gt; &lt;sequence&gt; &lt;digest&gt;</c>: puts a copy back in a new database; the exit code is the verdict, the lines carry no value.</summary>
internal static class RestoreLoadMode
{
    private const string Argument = "restore-load";
    private const int Positional = 4;
    private const int SequenceAt = 2;
    private const int DigestAt = 3;

    internal static bool Requested(string[] args) => args is [Argument, ..];

    /// <summary>The arguments the configuration reads: without the mode word and the three that name the copy and the tip.</summary>
    internal static string[] ConfigurationArguments(string[] args) => Requested(args) ? args[Math.Min(Positional, args.Length)..] : args;

    internal static async Task<HostLaunch> LaunchAsync(string[] args, DatabaseSettings database)
    {
        if (args.Length < Positional || !long.TryParse(args[SequenceAt], NumberStyles.None, CultureInfo.InvariantCulture, out var sequence))
        {
            await Console.Error.WriteLineAsync("restore-load pede a pasta da cópia e o topo anotado fora dela: sequência e digest.").ConfigureAwait(false);
            return HostLaunch.Loaded(ExitCode.ConfigurationRefused);
        }

        var result = await LoadAsync(database, args[1], new RestoreTip(sequence, args[DigestAt])).ConfigureAwait(false);
        await (result.Outcome == RestoreOutcome.Restored ? Console.Out : Console.Error).WriteLineAsync($"restauração: {result.Outcome} {result.Detail}".TrimEnd()).ConfigureAwait(false);
        return HostLaunch.Loaded(result.Outcome == RestoreOutcome.Restored ? ExitCode.Success : ExitCode.RestoreRefused);
    }

    private static async Task<RestoreResult> LoadAsync(DatabaseSettings database, string folder, RestoreTip tip)
    {
        try
        {
            await using var source = NpgsqlDataSource.Create(database.ConnectionString!);
            var verifier = new GuaranteeVerifier(source);
            return await new RestoreLoader(source, verifier, TimeProvider.System).LoadAsync(RestoreCopy.Open(folder), tip, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception failure) when (failure is NpgsqlException or IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return new RestoreResult(RestoreOutcome.CopyRefused, $"uma falha encerrou a carga ({failure.GetType().FullName})");
        }
    }
}
