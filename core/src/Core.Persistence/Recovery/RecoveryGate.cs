using Core.Persistence.Custody;
using Npgsql;
using NpgsqlTypes;

namespace Core.Persistence.Recovery;

/// <summary>
/// What the gate of the ingestion asks before it lets real material in (decision 0014): whether the recovery was rehearsed and recorded after the last loss of the master key declared,
/// by the order of the chain and never by the clock, and, when it was not, what is missing. The loss of the key of one label does not close the gate; only that of the whole master key does.
/// </summary>
public static class RecoveryGate
{
    private const string Tested =
        "SELECT EXISTS (SELECT 1 FROM public.custody_event WHERE step = @tested), "
        + "EXISTS (SELECT 1 FROM public.custody_event rehearsed WHERE rehearsed.step = @tested AND rehearsed.sequence > "
        + "COALESCE((SELECT max(lost.sequence) FROM public.custody_event lost WHERE lost.step = @lost AND lost.item_kind = 'master_key' AND lost.reference = @scope), 0))";

    /// <summary>The words that say what is missing while no rehearsal is recorded.</summary>
    public static string MissingWords { get; } =
        "falta o ensaio de recuperação: rode rehearse-recovery no ambiente limpo, com a cópia do banco e a cópia de guarda, e registre o recibo com record-recovery-test na produção (operacao/004)";

    /// <summary>The words that say what is missing while the only rehearsals recorded came before the last loss declared of the master key.</summary>
    public static string StaleWords { get; } =
        "o ensaio de recuperação registrado é anterior à última perda declarada da chave mestra: repita o ensaio com rehearse-recovery no ambiente limpo "
        + "e registre o recibo novo com record-recovery-test (operacao/004)";

    /// <summary>Whether a rehearsal came after the last loss of the master key declared, or, with no loss declared, whether any is in the chain.</summary>
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
            _ = command.Parameters.AddWithValue("tested", NpgsqlDbType.Text, CustodyNames.Of(CustodyStep.RecoveryTested));
            _ = command.Parameters.AddWithValue("lost", NpgsqlDbType.Text, CustodyNames.Of(CustodyStep.KeyLossDeclared));
            _ = command.Parameters.AddWithValue("scope", NpgsqlDbType.Text, KeyLossScope.MasterText);
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                _ = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
                return StatusOf(reader.GetBoolean(0), reader.GetBoolean(1));
            }
        }
    }

    private static RecoveryStatus StatusOf(bool any, bool afterTheLoss)
    {
        if (afterTheLoss)
        {
            return new RecoveryStatus(true, null);
        }

        return new RecoveryStatus(false, any ? StaleWords : MissingWords);
    }
}
