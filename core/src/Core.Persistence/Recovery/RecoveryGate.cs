using Core.Persistence.Custody;
using Npgsql;
using NpgsqlTypes;

namespace Core.Persistence.Recovery;

/// <summary>What the gate of the ingestion asks before it lets real material in (decision 0014): whether the recovery was rehearsed and recorded, and, when it was not, what is missing.</summary>
public static class RecoveryGate
{
    private const string Tested = "SELECT EXISTS (SELECT 1 FROM public.custody_event WHERE step = @step)";

    /// <summary>The words that say what is missing while no rehearsal is recorded.</summary>
    public static string MissingWords { get; } =
        "falta o ensaio de recuperação: rode rehearse-recovery no ambiente limpo, com a cópia do banco e a cópia de guarda, e registre o recibo com record-recovery-test na produção (operacao/004)";

    /// <summary>Whether the step of the rehearsal is in the chain.</summary>
    /// <param name="dataSource">The source of the connection.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    public static Task<RecoveryStatus> StatusAsync(NpgsqlDataSource dataSource, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        return ReadAsync(dataSource, cancellationToken);
    }

    private static async Task<RecoveryStatus> ReadAsync(NpgsqlDataSource dataSource, CancellationToken cancellationToken)
    {
        var command = dataSource.CreateCommand(Tested);
        await using (command.ConfigureAwait(false))
        {
            _ = command.Parameters.AddWithValue("step", NpgsqlDbType.Text, CustodyNames.Of(CustodyStep.RecoveryTested));
            var found = (bool)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
            return new RecoveryStatus(found, found ? null : MissingWords);
        }
    }
}
