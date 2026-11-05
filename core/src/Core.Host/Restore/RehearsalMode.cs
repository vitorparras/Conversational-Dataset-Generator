using Core.Host.Domains;
using Core.Persistence.Connection;
using Core.Persistence.Custody.Verification;
using Core.Persistence.ServerGuarantees;
using Core.Platform.Startup;
using Npgsql;

namespace Core.Host.Restore;

/// <summary>
/// The mode asked for with <c>rehearse-restore --seal &lt;text&gt;</c> (issue 66, operacao/006): the same executable, run once with the credential of the application over the database the copy was
/// put back into, rehearses the restore against the seal noted outside the copy and records whether it held, or what did not, in the chain. It opens no content, so no event of reading is written.
/// </summary>
internal static class RehearsalMode
{
    private const string Argument = "rehearse-restore";
    private const string SealOption = "--seal";
    private const string Refused = "recusado, nada foi gravado";

    internal static bool Requested(string[] args) => args is [Argument, ..];

    internal static async Task<HostLaunch> RunAsync(string[] args, TextWriter output, TextWriter error)
    {
        var (text, rest) = DomainCreationMode.Split(args, SealOption);
        var reading = ChainSeal.Read(text);
        if (reading.Seal is null)
        {
            await error.WriteLineAsync($"{Argument} {Refused}: informe o selo anotado fora da cópia, uma vez, com {SealOption} <selo>; {reading.Refusal}").ConfigureAwait(false);
            return HostLaunch.Ended(ExitCode.RehearsalRefused);
        }

        var loaded = HostConfiguration.Load(rest, migrating: true);
        var database = HostConfiguration.DatabaseOf(loaded, migrating: true);
        if (database is null || database.State == DatabaseSettingsState.Refused)
        {
            return HostLaunch.Refused(database?.Problems ?? loaded.Problems);
        }

        await using var dataSource = NpgsqlDataSource.Create(database.ConnectionString!);
        return HostLaunch.Ended(await RehearseAsync(dataSource, reading.Seal, new Words(output, error)).ConfigureAwait(false));
    }

    /// <summary>The rehearsal itself: it ends with success only if everything held and the result is in the chain.</summary>
    internal static async Task<ExitCode> RehearseAsync(NpgsqlDataSource dataSource, ChainSeal seal, Words words)
    {
        try
        {
            var result = await new RestoreRehearsal(dataSource, TimeProvider.System).RehearseAsync(seal, CancellationToken.None).ConfigureAwait(false);
            return await ReportAsync(result, words).ConfigureAwait(false);
        }
        catch (NpgsqlException)
        {
            await words.Error.WriteLineAsync($"{Argument} falhou ao falar com o banco, nada foi concluído").ConfigureAwait(false);
            return ExitCode.RehearsalNotRun;
        }
    }

    internal static async Task<ExitCode> ReportAsync(RehearsalResult result, Words words)
    {
        if (result.Recorded.Sequence is not { } sequence)
        {
            var cause = result.Passed ? "tudo valeu, mas a cadeia não aceitou o evento do resultado; repita" : $"falhou ({result.Names}) e a cadeia não aceitou o evento (operacao/009)";
            await words.Error.WriteLineAsync($"{Argument}: {cause}").ConfigureAwait(false);
            return ExitCode.RehearsalFailed;
        }

        if (result.Passed)
        {
            await words.Output.WriteLineAsync($"{Argument}: passou, na cadeia com a sequência {sequence}").ConfigureAwait(false);
            return ExitCode.Success;
        }

        await words.Error.WriteLineAsync($"{Argument}: falhou ({result.Names}), na cadeia com a sequência {sequence}; do selo: {result.Verification.Seal?.Statement}").ConfigureAwait(false);
        return ExitCode.RehearsalFailed;
    }

    /// <summary>Where the mode says what it did: the output for the success and the error for every failure.</summary>
    internal readonly record struct Words(TextWriter Output, TextWriter Error);
}
