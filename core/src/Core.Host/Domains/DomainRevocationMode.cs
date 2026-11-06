using Core.Persistence.Connection;
using Core.Persistence.Domains;
using Core.Platform.Startup;
using Npgsql;

namespace Core.Host.Domains;

/// <summary>
/// The mode asked for with <c>revoke-domain-label --label &lt;label&gt;</c> (issue 56): run once with the owner credential, it stops a label from encrypting anything new; what it sealed still opens.
/// </summary>
internal static class DomainRevocationMode
{
    private const string RevokeWord = "revoke-domain-label";
    private const string TargetOption = "--label";

    internal static bool Requested(string[] args) => args.Length > 0 && args[0] == RevokeWord;

    internal static async Task<HostLaunch> RunAsync(string[] args, TextWriter output, TextWriter error)
    {
        var (value, rest) = DomainCreationMode.Split(args, TargetOption);
        if (value is null)
        {
            await error.WriteLineAsync($"{RevokeWord} recusado: informe {TargetOption} uma vez, com o valor").ConfigureAwait(false);
            return HostLaunch.Ended(ExitCode.ConfigurationRefused);
        }

        var loaded = HostConfiguration.Load(rest, migrating: true);
        var database = HostConfiguration.DatabaseOf(loaded);
        if (database is null || database.State == DatabaseSettingsState.Refused)
        {
            return HostLaunch.Refused(database?.Problems ?? loaded.Problems);
        }

        await using var dataSource = NpgsqlDataSource.Create(database.ConnectionString!);
        return HostLaunch.Ended(await RevokeAsync(dataSource, value, new DomainGenerationMode.Words(output, error)).ConfigureAwait(false));
    }

    /// <summary>The revocation itself: the refusals and the failure each have a code of their own, and nothing is written in any of them.</summary>
    internal static async Task<ExitCode> RevokeAsync(NpgsqlDataSource dataSource, string target, DomainGenerationMode.Words words)
    {
        try
        {
            var result = await new DomainLabelRevocation(dataSource).RevokeAsync(target, CancellationToken.None).ConfigureAwait(false);
            return await ReportAsync(target, result, words).ConfigureAwait(false);
        }
        catch (NpgsqlException)
        {
            await words.Error.WriteLineAsync($"{RevokeWord} falhou ao falar com o banco, nada foi gravado").ConfigureAwait(false);
            return ExitCode.DomainLabelRevocationFailed;
        }
    }

    private static async Task<ExitCode> ReportAsync(string target, DomainLabelResult result, DomainGenerationMode.Words words)
    {
        if (result.Outcome == DomainLabelOutcome.Revoked)
        {
            await words.Output.WriteLineAsync($"{RevokeWord}: {target} revogado no domínio {result.DomainId}, na sequência {result.Sequence} da cadeia").ConfigureAwait(false);
            return ExitCode.Success;
        }

        await words.Error.WriteLineAsync($"{RevokeWord} recusado, nada foi gravado: {ReasonOf(result.Outcome)}").ConfigureAwait(false);
        return ExitCode.DomainLabelRevocationRefused;
    }

    private static string ReasonOf(DomainLabelOutcome outcome)
    {
        if (outcome == DomainLabelOutcome.LabelInvalid)
        {
            return "o texto não é um rótulo de derivação, que se escreve <finalidade>/<id do domínio>/v<n>";
        }
        if (outcome == DomainLabelOutcome.LabelUnknown)
        {
            return "nenhum rótulo registrado tem esse texto";
        }
        if (outcome == DomainLabelOutcome.AlreadyRevoked)
        {
            return "o rótulo já foi revogado";
        }
        return outcome == DomainLabelOutcome.LastGeneration
            ? "é a geração mais nova não revogada da finalidade, e o domínio ficaria sem rótulo para cifrar; rotacione antes, com rotate-domain-label"
            : "a gravação não se completou: outra chegou antes, ou a cadeia recusou o evento; repita o comando";
    }
}
