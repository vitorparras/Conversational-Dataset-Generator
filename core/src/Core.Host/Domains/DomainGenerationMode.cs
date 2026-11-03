using System.Globalization;
using Core.Persistence.Connection;
using Core.Persistence.Domains;
using Core.Platform.Keys;
using Core.Platform.Startup;
using Npgsql;

namespace Core.Host.Domains;

/// <summary>
/// The mode the operator asks for with <c>rotate-domain-label --domain &lt;id&gt;</c> (issue 56, decision 0045): run once with the owner credential, it passes the domain to the next generation
/// of its two labels if the custody file already holds them, and otherwise names the labels to add. It serves nothing and says nothing of a key.
/// </summary>
internal static class DomainGenerationMode
{
    private const string RotateWord = "rotate-domain-label";
    private const string DomainOption = "--domain";
    private const string Refused = "recusado, nada foi gravado";

    internal static bool Requested(string[] args) => args is [RotateWord, ..];

    internal static async Task<HostLaunch> RunAsync(string[] args, TextWriter output, TextWriter error)
    {
        var (value, rest) = OptionArguments.Once(args, DomainOption);
        if (value is null)
        {
            await error.WriteLineAsync($"{RotateWord} recusado: informe {DomainOption} uma vez, com o valor").ConfigureAwait(false);
            return HostLaunch.Ended(ExitCode.ConfigurationRefused);
        }

        var loaded = HostConfiguration.Load(rest, migrating: true);
        var database = HostConfiguration.DatabaseOf(loaded, migrating: true);
        if (database is null || database.State == DatabaseSettingsState.Refused)
        {
            return HostLaunch.Refused(database?.Problems ?? loaded.Problems);
        }

        var missing = MasterKeyConfiguration.Missing(loaded.Values).ToList();
        if (missing.Count > 0)
        {
            await error.WriteLineAsync($"{RotateWord} recusado: falta {string.Join(", ", missing)}, que a rotação exige").ConfigureAwait(false);
            return HostLaunch.Ended(ExitCode.ConfigurationRefused);
        }

        await using var dataSource = NpgsqlDataSource.Create(database.ConnectionString!);
        using var reading = await MasterKeyConfiguration.SourceOf(loaded.Values).ReadAsync(CancellationToken.None).ConfigureAwait(false);
        return HostLaunch.Ended(await RotateAsync(dataSource, reading, value, new Words(output, error)).ConfigureAwait(false));
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

        try
        {
            var result = await new DomainLabelRotation(dataSource).RotateAsync(id, reading.Labels, CancellationToken.None).ConfigureAwait(false);
            return await ReportAsync(result, words).ConfigureAwait(false);
        }
        catch (NpgsqlException)
        {
            await words.Error.WriteLineAsync($"{RotateWord} falhou ao falar com o banco, nada foi gravado").ConfigureAwait(false);
            return ExitCode.DomainLabelFailed;
        }
    }

    private static async Task<ExitCode> ReportAsync(DomainLabelResult result, Words words)
    {
        if (result.Outcome == DomainLabelOutcome.Rotated)
        {
            await words.Output.WriteLineAsync(
                $"{RotateWord}: o domínio {result.DomainId} cifra sob a geração v{result.Version} desde a sequência {result.Sequence} da cadeia").ConfigureAwait(false);
            return ExitCode.Success;
        }

        if (result.Outcome == DomainLabelOutcome.LabelsMissing)
        {
            await ReportMissingAsync(result, words).ConfigureAwait(false);
            return ExitCode.DomainLabelsMissing;
        }

        var reason = result.Outcome == DomainLabelOutcome.DomainNotFound
            ? "não há domínio com esse id"
            : "a gravação não se completou: outra chegou antes, ou a cadeia recusou o evento; repita o comando";
        await words.Error.WriteLineAsync($"{RotateWord} {Refused}: {reason}").ConfigureAwait(false);
        return ExitCode.DomainLabelRefused;
    }

    private static async Task ReportMissingAsync(DomainLabelResult result, Words words)
    {
        var named = string.Join(Environment.NewLine + "  ", result.MissingLabels);
        await words.Error.WriteLineAsync(
            $"{RotateWord} {Refused}: o domínio {result.DomainId} passaria à geração v{result.Version} e o arquivo de rótulos não traz, de rótulo de derivação:{Environment.NewLine}  {named}"
            + $"{Environment.NewLine}acrescente-os ao arquivo e à cópia de guarda (operacao/002) e rode de novo").ConfigureAwait(false);
    }

    internal readonly record struct Words(TextWriter Output, TextWriter Error);
}
