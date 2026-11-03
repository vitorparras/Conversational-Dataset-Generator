using System.Globalization;
using Core.Persistence.Connection;
using Core.Persistence.Domains;
using Core.Platform.Keys;
using Core.Platform.Startup;
using Npgsql;

namespace Core.Host.Domains;

/// <summary>
/// The modes the operator asks for with <c>rotate-domain-label --domain &lt;id&gt;</c> or <c>revoke-domain-label --label &lt;label&gt;</c> as the first arguments (issue 56, decision 0045): the same
/// executable, run once with the owner credential as the creation of a domain is. The rotation passes the domain to the next generation of its two labels if the custody file already holds them,
/// and otherwise names the labels to add; the revocation stops a label from encrypting anything new. Neither serves anything, has an endpoint, or says a key's content.
/// </summary>
internal static class DomainGenerationMode
{
    private const string RotateWord = "rotate-domain-label";
    private const string RevokeWord = "revoke-domain-label";
    private const string DomainOption = "--domain";
    private const string RevokedOption = "--label";
    private const string Refused = "recusado, nada foi gravado";

    private static readonly Dictionary<DomainLabelOutcome, string> Reasons = new()
    {
        { DomainLabelOutcome.DomainNotFound, "não há domínio com esse id" },
        { DomainLabelOutcome.LabelInvalid, "o texto não é um rótulo de derivação, que se escreve <finalidade>/<id do domínio>/v<n>" },
        { DomainLabelOutcome.LabelUnknown, "nenhum rótulo registrado tem esse texto" },
        { DomainLabelOutcome.AlreadyRevoked, "o rótulo já foi revogado" },
        { DomainLabelOutcome.LastGeneration, "é a geração mais nova não revogada da finalidade, e o domínio ficaria sem rótulo para cifrar; rotacione antes, com rotate-domain-label" },
        { DomainLabelOutcome.Conflict, "outra gravação chegou antes; repita o comando" },
        { DomainLabelOutcome.ChainRefused, "a cadeia recusou o evento" },
    };

    internal static bool Requested(string[] args) => args is [RotateWord or RevokeWord, ..];

    internal static async Task<HostLaunch> RunAsync(string[] args, TextWriter output, TextWriter error)
    {
        var revoking = args[0] == RevokeWord;
        var (value, rest) = OptionArguments.Once(args, revoking ? RevokedOption : DomainOption);
        if (value is null)
        {
            await error.WriteLineAsync($"{args[0]} recusado: informe {(revoking ? RevokedOption : DomainOption)} uma vez, com o valor").ConfigureAwait(false);
            return HostLaunch.Ended(ExitCode.ConfigurationRefused);
        }

        var loaded = HostConfiguration.Load(rest, migrating: true);
        var database = HostConfiguration.DatabaseOf(loaded, migrating: true);
        if (database is null || database.State == DatabaseSettingsState.Refused)
        {
            return HostLaunch.Refused(database?.Problems ?? loaded.Problems);
        }

        await using var dataSource = NpgsqlDataSource.Create(database.ConnectionString!);
        var words = new Words(output, error);
        if (revoking)
        {
            return HostLaunch.Ended(await RevokeAsync(dataSource, value, words).ConfigureAwait(false));
        }

        var missing = MasterKeyConfiguration.Missing(loaded.Values).ToList();
        if (missing.Count > 0)
        {
            await error.WriteLineAsync($"{RotateWord} recusado: falta {string.Join(", ", missing)}, que a rotação exige").ConfigureAwait(false);
            return HostLaunch.Ended(ExitCode.ConfigurationRefused);
        }

        using var reading = await MasterKeyConfiguration.SourceOf(loaded.Values).ReadAsync(CancellationToken.None).ConfigureAwait(false);
        return HostLaunch.Ended(await RotateAsync(dataSource, reading, value, words).ConfigureAwait(false));
    }

    /// <summary>The rotation itself, over a source that was read: the key is not read again, and nothing of it is written.</summary>
    internal static async Task<ExitCode> RotateAsync(NpgsqlDataSource dataSource, MasterKeyReading reading, string domain, Words words)
    {
        if (reading.Failure is not null)
        {
            await words.Error.WriteLineAsync($"{RotateWord} {Refused}: {reading.Failure.Found}").ConfigureAwait(false);
            return ExitCode.DomainLabelRefused;
        }

        if (!int.TryParse(domain, NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id < 1)
        {
            await words.Error.WriteLineAsync($"{RotateWord} {Refused}: o id do domínio é um inteiro positivo").ConfigureAwait(false);
            return ExitCode.DomainLabelRefused;
        }

        var rotation = new DomainLabelRotation(dataSource);
        return await GuardedAsync(RotateWord, words, () => rotation.RotateAsync(id, reading.Labels, CancellationToken.None)).ConfigureAwait(false);
    }

    /// <summary>The revocation itself.</summary>
    internal static Task<ExitCode> RevokeAsync(NpgsqlDataSource dataSource, string target, Words words) =>
        GuardedAsync(RevokeWord, words, () => new DomainLabelRotation(dataSource).RevokeAsync(target, CancellationToken.None));

    private static async Task<ExitCode> GuardedAsync(string word, Words words, Func<Task<DomainLabelResult>> act)
    {
        try
        {
            return await ReportAsync(word, await act().ConfigureAwait(false), words).ConfigureAwait(false);
        }
        catch (InvalidOperationException defect)
        {
            await words.Error.WriteLineAsync($"{word} {Refused}: {defect.Message}").ConfigureAwait(false);
            return ExitCode.DomainLabelRefused;
        }
        catch (NpgsqlException)
        {
            await words.Error.WriteLineAsync($"{word} falhou ao falar com o banco, nada foi gravado").ConfigureAwait(false);
            return ExitCode.DomainLabelFailed;
        }
    }

    private static async Task<ExitCode> ReportAsync(string word, DomainLabelResult result, Words words)
    {
        if (result.Outcome is DomainLabelOutcome.Rotated or DomainLabelOutcome.Revoked)
        {
            var said = result.Outcome == DomainLabelOutcome.Rotated
                ? $"{word}: o domínio {result.DomainId} cifra sob a geração v{result.Version} desde a sequência {result.Sequence} da cadeia"
                : $"{word}: revogada a geração v{result.Version} no domínio {result.DomainId}, na sequência {result.Sequence} da cadeia";
            await words.Output.WriteLineAsync(said).ConfigureAwait(false);
            return ExitCode.Success;
        }

        if (result.Outcome == DomainLabelOutcome.LabelsMissing)
        {
            await ReportMissingAsync(word, result, words).ConfigureAwait(false);
            return ExitCode.DomainLabelsMissing;
        }

        await words.Error.WriteLineAsync($"{word} {Refused}: {Reasons[result.Outcome]}").ConfigureAwait(false);
        return ExitCode.DomainLabelRefused;
    }

    private static async Task ReportMissingAsync(string word, DomainLabelResult result, Words words)
    {
        await words.Error.WriteLineAsync(
            $"{word} {Refused}: o domínio {result.DomainId} passaria à geração v{result.Version} e o arquivo de rótulos não traz, de rótulo de derivação:").ConfigureAwait(false);
        foreach (var label in result.MissingLabels)
        {
            await words.Error.WriteLineAsync($"  {label}").ConfigureAwait(false);
        }

        await words.Error.WriteLineAsync("acrescente-os ao arquivo e à cópia de guarda (operacao/002) e rode de novo").ConfigureAwait(false);
    }

    /// <summary>Where the mode says what it did: the output for the success and the error for every refusal.</summary>
    internal readonly record struct Words(TextWriter Output, TextWriter Error);
}
