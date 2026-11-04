using System.Diagnostics;
using Core.Persistence.Connection;
using Core.Persistence.Domains;
using Core.Platform.Keys;
using Core.Platform.Startup;
using Npgsql;

namespace Core.Host.Domains;

/// <summary>
/// The mode the operator asks for with <c>create-domain --name &lt;name&gt;</c> as the first arguments (issue 54, decision 0045): the same executable, run once with the owner credential as
/// the migration is, creates the domain if the custody file already holds the labels of its two purposes, and otherwise names the labels to add. It serves nothing, has no endpoint, and
/// says what it did without the name of the domain, a key or a label's content in the words.
/// </summary>
internal static class DomainCreationMode
{
    private const string Argument = "create-domain";
    private const string NameOption = "--name";
    private const string NamePrefix = NameOption + "=";

    internal static bool Requested(string[] args) => args is [Argument, ..];

    /// <summary>Takes the name out of the arguments that follow the mode word: <c>--name &lt;name&gt;</c> or <c>--name=&lt;name&gt;</c>, once; the rest goes to the configuration.</summary>
    /// <returns>The name and the arguments left, or a null name when it was not given exactly once.</returns>
    internal static (string? Name, string[] Remaining) Split(string[] args)
    {
        var rest = new List<string>();
        var names = new List<string>();
        var pending = new Queue<string>(args.Skip(1));
        while (pending.TryDequeue(out var current))
        {
            if (current == NameOption)
            {
                names.Add(pending.TryDequeue(out var value) ? value : string.Empty);
            }
            else if (current.StartsWith(NamePrefix, StringComparison.Ordinal))
            {
                names.Add(current[NamePrefix.Length..]);
            }
            else
            {
                rest.Add(current);
            }
        }

        var remaining = new string[rest.Count];
        rest.CopyTo(remaining);
        return (names.Count == 1 && names[0].Length > 0 ? names[0] : null, remaining);
    }

    internal static async Task<HostLaunch> RunAsync(string[] args, TextWriter output, TextWriter error)
    {
        var (name, rest) = Split(args);
        if (name is null)
        {
            await error.WriteLineAsync("create-domain recusado: informe o nome uma vez, com --name <nome>").ConfigureAwait(false);
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
            await error.WriteLineAsync($"create-domain recusado: falta {string.Join(", ", missing)}, que a criação exige").ConfigureAwait(false);
            return HostLaunch.Ended(ExitCode.ConfigurationRefused);
        }

        using var reading = await MasterKeyConfiguration.SourceOf(loaded.Values).ReadAsync(CancellationToken.None).ConfigureAwait(false);
        await using var dataSource = NpgsqlDataSource.Create(database.ConnectionString!);
        return HostLaunch.Ended(await CreateAsync(dataSource, reading, name, new Words(output, error)).ConfigureAwait(false));
    }

    /// <summary>The creation itself, over a source that was read: the key is not read again, and nothing of it is written.</summary>
    internal static async Task<ExitCode> CreateAsync(NpgsqlDataSource dataSource, MasterKeyReading reading, string name, Words words)
    {
        var (output, error) = (words.Output, words.Error);
        if (reading.Failure is not null)
        {
            await error.WriteLineAsync($"create-domain recusado: {reading.Failure.Found}").ConfigureAwait(false);
            return ExitCode.DomainCreationRefused;
        }

        try
        {
            var result = await new DomainCreation(dataSource).CreateAsync(name, reading.Labels, CancellationToken.None).ConfigureAwait(false);
            return await ReportAsync(result, output, error).ConfigureAwait(false);
        }
        catch (InvalidOperationException defect)
        {
            await error.WriteLineAsync($"create-domain recusado, nada foi gravado: {defect.Message}").ConfigureAwait(false);
            return ExitCode.DomainCreationRefused;
        }
        catch (NpgsqlException)
        {
            await error.WriteLineAsync("create-domain falhou ao falar com o banco, nada foi gravado").ConfigureAwait(false);
            return ExitCode.DomainCreationFailed;
        }
    }

    private static async Task<ExitCode> ReportAsync(DomainCreationResult result, TextWriter output, TextWriter error)
    {
        switch (result.Outcome)
        {
            case DomainCreationOutcome.Created:
                await output.WriteLineAsync($"create-domain: domínio criado com o id {result.DomainId}, na cadeia com a sequência {result.Sequence}").ConfigureAwait(false);
                return ExitCode.Success;
            case DomainCreationOutcome.LabelsMissing:
                await error.WriteLineAsync(
                    $"create-domain recusado, nada foi gravado: o domínio teria o id {result.DomainId} e o arquivo de rótulos não traz, de rótulo de derivação:").ConfigureAwait(false);
                foreach (var label in result.MissingLabels)
                {
                    await error.WriteLineAsync($"  {label}").ConfigureAwait(false);
                }

                await error.WriteLineAsync("acrescente-os ao arquivo e à cópia de guarda (operacao/002) e rode de novo").ConfigureAwait(false);
                return ExitCode.DomainLabelsMissing;
            case DomainCreationOutcome.NameTaken:
                await error.WriteLineAsync("create-domain recusado, nada foi gravado: já existe um domínio com esse nome").ConfigureAwait(false);
                return ExitCode.DomainNameTaken;
            case DomainCreationOutcome.NameInvalid:
                await error.WriteLineAsync("create-domain recusado, nada foi gravado: o nome não pode ser vazio nem ter espaço nas pontas").ConfigureAwait(false);
                return ExitCode.DomainCreationRefused;
            case DomainCreationOutcome.Conflict:
                await error.WriteLineAsync("create-domain recusado, nada foi gravado: outra gravação chegou antes; repita o comando").ConfigureAwait(false);
                return ExitCode.DomainCreationRefused;
            case DomainCreationOutcome.ChainRefused:
                await error.WriteLineAsync($"create-domain recusado, nada foi gravado: a cadeia recusou o evento ({result.ChainOutcome})").ConfigureAwait(false);
                return ExitCode.DomainCreationRefused;
            default:
                throw new UnreachableException();
        }
    }

    /// <summary>Where the mode says what it did: the output for the success and the error for every refusal.</summary>
    internal readonly record struct Words(TextWriter Output, TextWriter Error);
}
