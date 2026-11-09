using Core.Persistence.Connection;
using Core.Persistence.Custody.Verification;
using Core.Persistence.People;
using Core.Persistence.Settings;
using Core.Platform.Configuration;
using Core.Platform.Configuration.Sources;
using Core.Platform.Startup;
using Npgsql;

namespace Core.Host.Settings;

/// <summary>
/// The mode asked for with <c>apply-configuration</c> (issue 634, operacao/049; decision 0053): the same executable, run once by the service manager under the account that writes the file of the
/// configuration and with a credential of the database that only reads. It applies the approved changes the chain and the registry still stand behind, restarts the core once if the file changed
/// or a restart is still owed, and says what it did in its output; whatever it does not apply is told in the log with the reason and the two sequences, never a person.
/// </summary>
internal static class ApplyConfigurationMode
{
    private const string Argument = "apply-configuration";

    internal static bool Requested(string[] args) => args is [Argument, ..];

    internal static Task<HostLaunch> RunAsync(string[] args, TextWriter output, TextWriter error) => RunAsync(args[1..], ApplierConfiguration.FilePath, output, error);

    internal static async Task<HostLaunch> RunAsync(string[] args, string file, TextWriter output, TextWriter error)
    {
        var loaded = ClosedConfiguration.Load(ApplierConfiguration.Schema, new JsonFileSource(file), EnvironmentSource.FromProcess(), new StrictArgumentsSource(args));
        var database = HostConfiguration.DatabaseOf(loaded);
        if (database is null || database.State == DatabaseSettingsState.Refused)
        {
            return HostLaunch.Refused(database?.Problems ?? loaded.Problems);
        }

        var refusals = ApplierConfiguration.Refusals(loaded.Values);
        if (refusals.Count > 0)
        {
            return HostLaunch.Refused(refusals);
        }

        await using var dataSource = NpgsqlDataSource.Create(database.ConnectionString!);
        return HostLaunch.Ended(await ApplyAsync(dataSource, loaded.Values, output, error).ConfigureAwait(false));
    }

    private static async Task<ExitCode> ApplyAsync(NpgsqlDataSource dataSource, IReadOnlyDictionary<string, string> values, TextWriter output, TextWriter error)
    {
        try
        {
            if (await CredentialPrivileges.CanWriteAsync(dataSource, CancellationToken.None).ConfigureAwait(false))
            {
                await error.WriteLineAsync($"{Argument} recusado, nada foi gravado: a credencial do banco do aplicador grava; use uma que só lê (operacao/049)").ConfigureAwait(false);
                return ExitCode.ApplierRefused;
            }

            var owed = await AppliedAsync(dataSource, values, error).ConfigureAwait(false);
            if (owed == 0)
            {
                await output.WriteLineAsync($"{Argument}: nada a aplicar").ConfigureAwait(false);
                return ExitCode.Success;
            }

            return await RestartedAsync(values, owed, output, error).ConfigureAwait(false);
        }
        catch (NpgsqlException)
        {
            await error.WriteLineAsync($"{Argument} falhou ao falar com o banco, nada foi concluído").ConfigureAwait(false);
            return ExitCode.ApplierFailed;
        }
    }

    private static async Task<int> AppliedAsync(NpgsqlDataSource dataSource, IReadOnlyDictionary<string, string> values, TextWriter error)
    {
        using var loggers = LoggerFactory.Create(builder => builder.AddProvider(new WriterLogProvider(error)));
        var file = new ConfigurationFile(values[ApplierConfiguration.SettingsFilePath], TimeProvider.System);
        var applier = new ConfigurationApplier(file, loggers);
        var facts = new ApplicationFactsReader(new CustodyVerifier(dataSource, TimeProvider.System), new PersonRegistry(dataSource));
        var owed = 0;
        foreach (var change in await new ApprovedChanges(dataSource).PendingAsync(CancellationToken.None).ConfigureAwait(false))
        {
            var refusal = applier.Apply(change, await facts.ReadAsync(change, CancellationToken.None).ConfigureAwait(false), file.Values());
            owed += refusal == ApplicationRefusal.None ? 1 : 0;
        }

        return owed;
    }

    private static async Task<ExitCode> RestartedAsync(IReadOnlyDictionary<string, string> values, int applied, TextWriter output, TextWriter error)
    {
        var restart = new ServiceRestart(values[ApplierConfiguration.RestartCommandPath], ApplierConfiguration.RestartArguments(values));
        if (await restart.RunAsync(CancellationToken.None).ConfigureAwait(false))
        {
            await output.WriteLineAsync($"{Argument}: {applied} alteração(ões) no arquivo e o núcleo reiniciado").ConfigureAwait(false);
            return ExitCode.Success;
        }

        await error.WriteLineAsync($"{Argument}: {applied} alteração(ões) no arquivo, mas o núcleo NÃO reiniciou; a próxima execução tenta de novo").ConfigureAwait(false);
        return ExitCode.ApplierFailed;
    }
}
