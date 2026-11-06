using System.Globalization;
using Core.Persistence.Connection;
using Core.Persistence.Content.Rotation;
using Core.Platform.Keys;
using Core.Platform.Startup;
using Npgsql;

namespace Core.Host.Domains;

/// <summary>The mode of <c>rewrap-domain --domain &lt;id&gt;</c> (issue 56): once, with the owner credential, it wraps the keys of the domain again in batches; <c>--status</c> only reads.</summary>
internal static class DomainRewrapMode
{
    private const string RewrapWord = "rewrap-domain";
    private const string DomainOption = "--domain";
    private const string StatusFlag = "--status";
    private const int WholeBatch = 500;

    internal static bool Requested(string[] args) => args.Length > 0 && args[0] == RewrapWord;

    internal static async Task<HostLaunch> RunAsync(string[] args, TextWriter output, TextWriter error)
    {
        var (value, rest) = DomainCreationMode.Split(args, DomainOption);
        if (value is null)
        {
            await error.WriteLineAsync($"{RewrapWord} recusado: informe {DomainOption} uma vez, com o valor").ConfigureAwait(false);
            return HostLaunch.Ended(ExitCode.ConfigurationRefused);
        }

        var loaded = HostConfiguration.Load(Array.FindAll(rest, argument => argument != StatusFlag), migrating: true);
        var database = HostConfiguration.DatabaseOf(loaded);
        if (database is null || database.State == DatabaseSettingsState.Refused)
        {
            return HostLaunch.Refused(database?.Problems ?? loaded.Problems);
        }

        var missing = MasterKeyConfiguration.Missing(loaded.Values).ToList();
        if (missing.Count > 0)
        {
            await error.WriteLineAsync($"{RewrapWord} recusado: falta {string.Join(", ", missing)}, que a recifragem exige").ConfigureAwait(false);
            return HostLaunch.Ended(ExitCode.ConfigurationRefused);
        }

        await using var dataSource = NpgsqlDataSource.Create(database.ConnectionString!);
        using var reading = await MasterKeyConfiguration.SourceOf(loaded.Values).ReadAsync(CancellationToken.None).ConfigureAwait(false);
        return HostLaunch.Ended(await RewrapAsync(dataSource, reading, (value, rest.Contains(StatusFlag)), new DomainGenerationMode.Words(output, error)).ConfigureAwait(false));
    }

    internal static async Task<ExitCode> RewrapAsync(NpgsqlDataSource dataSource, MasterKeyReading reading, (string Domain, bool Status) request, DomainGenerationMode.Words words)
    {
        if (reading.Failure is not null)
        {
            await words.Error.WriteLineAsync($"{RewrapWord} recusado: {reading.Failure.Found}").ConfigureAwait(false);
            return ExitCode.DomainRewrapRefused;
        }

        if (!int.TryParse(request.Domain, NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id < 1)
        {
            await words.Error.WriteLineAsync($"{RewrapWord} recusado: o id do domínio é um inteiro positivo").ConfigureAwait(false);
            return ExitCode.DomainRewrapRefused;
        }

        try
        {
            await words.Output.WriteLineAsync(await RunOnceAsync(dataSource, reading, (id, request.Status)).ConfigureAwait(false)).ConfigureAwait(false);
            return ExitCode.Success;
        }
        catch (Exception problem) when (problem is EnvelopeRefusedException or InvalidOperationException or NpgsqlException)
        {
            var failed = problem is NpgsqlException;
            var said = failed ? "falhou ao falar com o banco" : $"recusado: {problem.Message}";
            await words.Error.WriteLineAsync($"{RewrapWord} {said}; os lotes já gravados ficam, e repetir o comando continua dali").ConfigureAwait(false);
            return failed ? ExitCode.DomainRewrapFailed : ExitCode.DomainRewrapRefused;
        }
    }

    private static async Task<string> RunOnceAsync(NpgsqlDataSource dataSource, MasterKeyReading reading, (int Id, bool Status) request)
    {
        var rewrap = new DomainRewrap(dataSource, WholeBatch);
        if (request.Status)
        {
            var (current, behind) = await rewrap.StatusAsync(request.Id, reading.Labels, CancellationToken.None).ConfigureAwait(false);
            return $"{RewrapWord}: domínio {request.Id}: {current} chave(s) na geração mais nova não revogada, {behind} por reembrulhar";
        }

        var done = await rewrap.RewrapAsync(request.Id, CompositionRoot.KeysOf(reading), reading.Labels, CancellationToken.None).ConfigureAwait(false);
        return $"{RewrapWord}: {done} chave(s) do domínio {request.Id} reembrulhada(s) para a geração mais nova não revogada";
    }
}
