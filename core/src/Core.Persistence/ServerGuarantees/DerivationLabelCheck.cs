using Core.Platform.Keys;
using Core.Platform.Startup;
using Npgsql;

namespace Core.Persistence.ServerGuarantees;

/// <summary>
/// Refuses to start when a derivation label already used is not among the ones provided with the key, and names the label that is
/// missing, instead of leaving the data under it to look unreadable. The table is read with the application's credential.
/// </summary>
public sealed class DerivationLabelCheck : IStartupCheck
{
    private const string Query = "SELECT coalesce(pg_catalog.array_agg(label ORDER BY label), ARRAY[]::text[]) AS labels FROM public.derivation_label";

    private readonly NpgsqlDataSource dataSource;
    private readonly IMasterKeySource source;

    /// <summary>The check reads the labels provided through the source and the labels used through the data source of the application's credential.</summary>
    public DerivationLabelCheck(NpgsqlDataSource dataSource, IMasterKeySource source)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentNullException.ThrowIfNull(source);
        this.dataSource = dataSource;
        this.source = source;
    }

    /// <inheritdoc />
    public string Name => "DerivationLabels";

    /// <inheritdoc />
    public async Task<CheckResult> RunAsync(CancellationToken cancellationToken)
    {
        using var reading = await source.ReadAsync(cancellationToken).ConfigureAwait(false);
        return reading.Failure is null
            ? await ProbeAsync(reading.Labels, cancellationToken).ConfigureAwait(false)
            : CheckResult.Failed(Name, "rótulos não conferidos: a chave-mestra e os rótulos que a acompanham não foram lidos", source.Origin);
    }

    internal static Finding Judge(DerivationLabels provided, IReadOnlyCollection<string> used)
    {
        var lacking = provided.Lacking(used);
        return lacking.Count == 0
            ? Finding.Holds($"{used.Count} rótulo(s) de derivação em uso, todos no conjunto fornecido com a chave")
            : Finding.Missing(
                $"rótulo(s) de derivação em uso ausente(s) do conjunto fornecido com a chave: {string.Join(", ", lacking)}; "
                + "forneça o conjunto completo de rótulos da cópia de guarda, porque sem o rótulo o dado cifrado sob ele fica ilegível");
    }

    private Task<CheckResult> ProbeAsync(DerivationLabels provided, CancellationToken cancellationToken) =>
        ServerProbe.RunAsync(
            dataSource,
            new Guarantee(
                Name,
                "rode o modo migrate com a credencial de dono, que cria public.derivation_label, e conceda leitura dela ao papel da aplicação",
                (connection, token) => ReadAsync(connection, provided, token),
                ReadsCatalog: false),
            cancellationToken);

    private static Task<Finding> ReadAsync(NpgsqlConnection connection, DerivationLabels provided, CancellationToken cancellationToken) =>
        ServerProbe.ReadRowAsync(connection, Query, row => Judge(provided, (string[])row["labels"]), cancellationToken);
}
