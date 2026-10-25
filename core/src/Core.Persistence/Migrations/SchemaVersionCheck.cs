using System.Diagnostics;
using Core.Persistence.ServerGuarantees;
using Core.Platform.Startup;
using Npgsql;

namespace Core.Persistence.Migrations;

/// <summary>
/// Checks, with the application's credential, that the schema is at the version this build expects; an absent registry counts
/// as version 0. Behind, ahead or diverging, it refuses and says to run the migration mode, which this check never does.
/// </summary>
public sealed class SchemaVersionCheck : IStartupCheck
{
    private readonly NpgsqlDataSource dataSource;
    private readonly IReadOnlyList<MigrationStep> steps;

    /// <summary>The check reads through the data source built with the application's credential.</summary>
    public SchemaVersionCheck(NpgsqlDataSource dataSource, IReadOnlyList<MigrationStep> steps)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentNullException.ThrowIfNull(steps);
        this.dataSource = dataSource;
        this.steps = steps;
    }

    /// <inheritdoc />
    public string Name => "SchemaVersion";

    /// <inheritdoc />
    public Task<CheckResult> RunAsync(CancellationToken cancellationToken) =>
        ServerProbe.RunAsync(
            dataSource,
            new Guarantee(Name, $"o papel da aplicação precisa ler {SchemaRegistry.Table}, que a migração concede a PUBLIC", ReadAsync),
            cancellationToken);

    internal static Finding Judge(SchemaComparison comparison) => comparison.State switch
    {
        SchemaState.UpToDate => Finding.Holds($"esquema na versão {comparison.Found}, a que esta instalação espera"),
        SchemaState.Behind => Finding.Missing(
            $"esquema na versão {comparison.Found}, esta instalação espera a {comparison.Expected}: rode o modo migrate com a credencial de dono"),
        SchemaState.Ahead => Finding.Missing(
            $"esquema na versão {comparison.Found}, à frente da {comparison.Expected} que esta instalação conhece: instale a versão do sistema que a conhece"),
        SchemaState.Divergent => Finding.Missing(
            $"o passo {comparison.Step} foi aplicado com conteúdo diferente do desta instalação (registrado {comparison.Registered}, atual {comparison.Current}): "
            + "não rode o modo migrate; descubra quem alterou o passo ou instale a versão do sistema que o aplicou"),
        _ => throw new UnreachableException(),
    };

    private async Task<Finding> ReadAsync(NpgsqlConnection connection, CancellationToken cancellationToken) =>
        Judge(SchemaComparison.Compare(steps, await SchemaRegistry.ReadAsync(connection, cancellationToken).ConfigureAwait(false)));
}
