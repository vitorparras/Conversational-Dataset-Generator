using Core.Persistence.Connection;
using Core.Persistence.Custody;
using Core.Persistence.Custody.Chain;
using Core.Platform.Startup;
using Npgsql;

namespace Core.Host.Loss;

/// <summary>
/// The mode asked for with <c>declare-key-loss --scope &lt;master|label&gt; --tried &lt;list&gt;</c> (issue 67, decision 0014): the same executable, run once,
/// writes the declaration of the loss of the key to the chain and serves nothing. It needs no key, since what is declared is that there is none to read;
/// the scope and what was tried come from fixed lists, and the declaration has no way back.
/// </summary>
internal static class KeyLossMode
{
    private const string Argument = "declare-key-loss";
    private const string ScopeOption = "--scope";
    private const string TriedOption = "--tried";
    private const string Refused = "recusado, nada foi gravado";

    internal static bool Requested(string[] args) => args is [Argument, ..];

    internal static async Task<HostLaunch> RunAsync(string[] args, TextWriter output, TextWriter error)
    {
        var (scope, afterScope) = Split(args[..1], args[1..], ScopeOption);
        var (tried, rest) = Split(args[..1], afterScope, TriedOption);
        if (scope is null || tried is null)
        {
            await error.WriteLineAsync($"{Argument} recusado: informe {ScopeOption} e {TriedOption}, uma vez cada, com o valor").ConfigureAwait(false);
            return HostLaunch.Ended(ExitCode.ConfigurationRefused);
        }

        var loaded = HostConfiguration.Load(rest, migrating: true);
        var database = HostConfiguration.DatabaseOf(loaded);
        if (database is null || database.State == DatabaseSettingsState.Refused)
        {
            return HostLaunch.Refused(database?.Problems ?? loaded.Problems);
        }

        await using var dataSource = NpgsqlDataSource.Create(database.ConnectionString!);
        return HostLaunch.Ended(await DeclareAsync(dataSource, scope, tried, new Words(output, error)).ConfigureAwait(false));
    }

    /// <summary>The declaration itself: what is outside the lists is refused before the chain is touched.</summary>
    internal static async Task<ExitCode> DeclareAsync(NpgsqlDataSource dataSource, string scope, string tried, Words words)
    {
        if (!KeyLossScope.TryRead(scope, out var read) || read is null)
        {
            await words.Error.WriteLineAsync($"{Argument} {Refused}: o escopo é master ou um rótulo de derivação").ConfigureAwait(false);
            return ExitCode.KeyLossRefused;
        }

        if (!KeyLossAttemptNames.TryRead(tried, out var attempts))
        {
            await words.Error.WriteLineAsync($"{Argument} {Refused}: o que foi tentado é uma lista, sem repetir, de {string.Join(", ", KeyLossAttemptNames.Accepted)}").ConfigureAwait(false);
            return ExitCode.KeyLossRefused;
        }

        try
        {
            var result = await new CustodyChain(dataSource).AppendAsync(CustodyEvent.KeyLossDeclared(read, attempts), CancellationToken.None).ConfigureAwait(false);
            return await ReportAsync(result, read, words).ConfigureAwait(false);
        }
        catch (NpgsqlException)
        {
            await words.Error.WriteLineAsync($"{Argument} falhou ao falar com o banco, nada foi gravado").ConfigureAwait(false);
            return ExitCode.KeyLossFailed;
        }
    }

    private static async Task<ExitCode> ReportAsync(CustodyResult result, KeyLossScope scope, Words words)
    {
        if (result.Outcome == CustodyOutcome.Appended)
        {
            await words.Output.WriteLineAsync(
                $"{Argument}: perda declarada para {scope.Text}, na cadeia com a sequência {result.Sequence}; não há volta, a saída é uma chave nova (operacao/003)").ConfigureAwait(false);
            return ExitCode.Success;
        }

        await words.Error.WriteLineAsync($"{Argument} {Refused}: a cadeia não aceitou o evento ({result.Outcome}); repita o comando").ConfigureAwait(false);
        return ExitCode.KeyLossRefused;
    }

    private static (string? Value, string[] Remaining) Split(string[] mode, string[] args, string option)
    {
        var joined = new string[mode.Length + args.Length];
        mode.CopyTo(joined, 0);
        args.CopyTo(joined, mode.Length);
        return Domains.DomainCreationMode.Split(joined, option);
    }

    /// <summary>Where the mode says what it did: the output for the success and the error for every refusal.</summary>
    internal readonly record struct Words(TextWriter Output, TextWriter Error);
}
