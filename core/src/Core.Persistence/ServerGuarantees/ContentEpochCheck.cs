using System.Data.Common;
using System.Globalization;
using Core.Persistence.Content;
using Core.Platform.Keys;
using Core.Platform.Startup;
using Npgsql;

namespace Core.Persistence.ServerGuarantees;

/// <summary>
/// Refuses to start when a row of content not emptied is wrapped by an epoch that the epochs file does not hold, and names the epoch that is missing: a copy of the database taken
/// before a rotation, restored beside an epochs file made after it, ends here and not in content that looks unreadable (decision 0049).
/// </summary>
public sealed class ContentEpochCheck : IStartupCheck
{
    private const string Domains = "SELECT id FROM public.domain ORDER BY id";
    private const string Declare = "SELECT pg_catalog.set_config('app.domain', @domain, true), pg_catalog.set_config('app.content_scope', 'system', true)";

    private readonly NpgsqlDataSource dataSource;
    private readonly IMasterKeySource source;

    /// <summary>The check reads the epochs provided through the source and the epochs used through the data source of the application's credential.</summary>
    public ContentEpochCheck(NpgsqlDataSource dataSource, IMasterKeySource source)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentNullException.ThrowIfNull(source);
        this.dataSource = dataSource;
        this.source = source;
    }

    /// <inheritdoc />
    public string Name => "ContentEpochs";

    /// <inheritdoc />
    public async Task<CheckResult> RunAsync(CancellationToken cancellationToken)
    {
        using var reading = await source.ReadAsync(cancellationToken).ConfigureAwait(false);
        return reading.Failure is null
            ? await ProbeAsync(reading.Epochs, cancellationToken).ConfigureAwait(false)
            : CheckResult.Failed(Name, "épocas não conferidas: a chave-mestra e os arquivos que a acompanham não foram lidos", source.Origin);
    }

    internal static Finding Judge(CustodyEpochs provided, IReadOnlyCollection<int> used)
    {
        var lacking = provided.Lacking(used).ToArray();
        return lacking.Length == 0
            ? Finding.Holds($"{used.Count} época(s) em uso por chaves de item, todas no arquivo de épocas")
            : Finding.Missing(
                $"época(s) em uso ausente(s) do arquivo de épocas: {string.Join(", ", lacking)}; "
                + "o banco foi restaurado de uma cópia anterior a uma rotação, ou o arquivo de épocas é de outra instalação, e o conteúdo sob essa época não abre");
    }

    private Task<CheckResult> ProbeAsync(CustodyEpochs provided, CancellationToken cancellationToken) =>
        ServerProbe.RunAsync(
            dataSource,
            new Guarantee(
                Name,
                "rode o modo migrate com a credencial de dono, que cria as colunas das chaves de item, e conceda leitura delas ao papel da aplicação",
                async (connection, token) => Judge(provided, await UsedAsync(connection, token).ConfigureAwait(false)),
                ReadsCatalog: false),
            cancellationToken);

    private static async Task<HashSet<int>> UsedAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        var used = new HashSet<int>();
        var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            foreach (var domain in await DomainsAsync(connection, cancellationToken).ConfigureAwait(false))
            {
                await DeclareAsync(connection, domain, cancellationToken).ConfigureAwait(false);
                used.UnionWith(await ReadUsedAsync(connection, cancellationToken).ConfigureAwait(false));
            }
        }

        return used;
    }

    private static async Task DeclareAsync(NpgsqlConnection connection, int domain, CancellationToken cancellationToken)
    {
        var declare = new NpgsqlCommand(Declare, connection);
        await using (declare.ConfigureAwait(false))
        {
            _ = declare.Parameters.AddWithValue("domain", domain.ToString(CultureInfo.InvariantCulture));
            _ = await declare.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<List<int>> DomainsAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        var command = new NpgsqlCommand(Domains, connection);
        await using (command.ConfigureAwait(false))
        {
            return await IntegersAsync(command, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<List<int>> ReadUsedAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        var command = new NpgsqlCommand(ContentQueries.ReadEpochsInUse, connection);
        await using (command.ConfigureAwait(false))
        {
            return await IntegersAsync(command, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<List<int>> IntegersAsync(NpgsqlCommand command, CancellationToken cancellationToken)
    {
        var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        await using (reader.ConfigureAwait(false))
        {
            return await CollectAsync(reader, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<List<int>> CollectAsync(DbDataReader reader, CancellationToken cancellationToken)
    {
        var found = new List<int>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            found.Add(reader.GetInt32(0));
        }

        return found;
    }
}
