using Core.Host.Domains;
using Core.Persistence.Connection;
using Core.Persistence.ServerGuarantees;
using Core.Platform.Configuration;
using Core.Platform.Startup;
using Npgsql;

namespace Core.Host.Restore;

/// <summary>
/// The mode asked for with <c>copy-data --to &lt;folder&gt;</c> (issue 66, operacao/005): the same executable, run once with the owner credential, records the seal of the chain and writes the copy of
/// the data in the folder, with the configuration without its secrets. It reads no key and no file of the custody, so the copy has none of them, and it prints the seal to be noted outside the copy.
/// </summary>
internal static class CopyDataMode
{
    private const string Argument = "copy-data";
    private const string ToOption = "--to";
    private const string Refused = "recusado, nada foi gravado";

    internal static bool Requested(string[] args) => args is [Argument, ..];

    internal static async Task<HostLaunch> RunAsync(string[] args, TextWriter output, TextWriter error)
    {
        var (folder, rest) = DomainCreationMode.Split(args, ToOption);
        if (folder is null)
        {
            await error.WriteLineAsync($"{Argument} {Refused}: informe a pasta da cópia uma vez, com {ToOption} <pasta>").ConfigureAwait(false);
            return HostLaunch.Ended(ExitCode.ConfigurationRefused);
        }

        var loaded = HostConfiguration.Load(rest, migrating: true);
        var database = HostConfiguration.DatabaseOf(loaded, migrating: true);
        if (database is null || database.State == DatabaseSettingsState.Refused)
        {
            return HostLaunch.Refused(database?.Problems ?? loaded.Problems);
        }

        await using var dataSource = NpgsqlDataSource.Create(database.ConnectionString!);
        return HostLaunch.Ended(await CopyAsync(dataSource, new Request(folder, loaded.View.Entries), new Words(output, error)).ConfigureAwait(false));
    }

    /// <summary>The copy itself: what it makes is told in the output, with the seal, and every refusal in the error.</summary>
    internal static async Task<ExitCode> CopyAsync(NpgsqlDataSource dataSource, Request request, Words words)
    {
        try
        {
            var result = await new DataCopy(dataSource, TimeProvider.System).WriteAsync(request.Folder, request.Configuration, CancellationToken.None).ConfigureAwait(false);
            return await ReportAsync(result, words).ConfigureAwait(false);
        }
        catch (Exception failure) when (failure is NpgsqlException or IOException or UnauthorizedAccessException)
        {
            await words.Error.WriteLineAsync($"{Argument} falhou ao falar com o banco ou ao escrever a pasta; apague o que ficou nela e repita").ConfigureAwait(false);
            return ExitCode.CopyFailed;
        }
    }

    internal static async Task<ExitCode> ReportAsync(DataCopyResult result, Words words)
    {
        if (result.Outcome == DataCopyOutcome.Made)
        {
            await words.Output.WriteLineAsync($"{Argument}: cópia escrita; anote fora dela, com a data, em registro que o banco não alcance (operacao/005), o topo dela:").ConfigureAwait(false);
            await words.Output.WriteLineAsync(result.Seal!.ToText()).ConfigureAwait(false);
            await words.Output.WriteLineAsync($"restore-load <pasta> {result.Seal.Events} {result.Seal.Digest}").ConfigureAwait(false);
            return ExitCode.Success;
        }

        await words.Error.WriteLineAsync($"{Argument} {Refused}: {ReasonOf(result.Outcome)}").ConfigureAwait(false);
        return ExitCode.CopyRefused;
    }

    private static string ReasonOf(DataCopyOutcome outcome) => outcome == DataCopyOutcome.FolderNotEmpty
        ? "a pasta já tem conteúdo, e uma cópia não se mistura com o que está lá"
        : "a cadeia não aceitou o selo (confira a última verificação, 007) ou outra gravação chegou antes da leitura (repita)";

    /// <summary>What the copy is of: the folder it goes to and the configuration in force as it may be shown.</summary>
    internal sealed record Request(string Folder, IReadOnlyList<EffectiveEntry> Configuration);

    /// <summary>Where the mode says what it did: the output for the success and the error for every refusal.</summary>
    internal readonly record struct Words(TextWriter Output, TextWriter Error);
}
