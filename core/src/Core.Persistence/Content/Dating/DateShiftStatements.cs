using Core.Persistence.Content.Sealing;
using Npgsql;
using NpgsqlTypes;

namespace Core.Persistence.Content.Dating;

/// <summary>What the shift of the dates asks of the database inside the transaction of the domain: the row it holds, as stored, and the row of a shift drawn.</summary>
internal static class DateShiftStatements
{
    private const int ShiftColumn = 0;
    private const int EpochColumn = 1;
    private const int WrappedColumn = 2;

    /// <summary>The sealed shift and the key of its row, or none when the domain holds no row.</summary>
    internal static async Task<SealedShift?> ReadAsync(ContentTransaction transaction, CancellationToken cancellationToken)
    {
        var command = transaction.CreateCommand(ContentQueries.ReadDateShift);
        await using (command.ConfigureAwait(false))
        {
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
                    ? new SealedShift((byte[])reader.GetValue(ShiftColumn), new ContentKey(reader.GetInt32(EpochColumn), (byte[])reader.GetValue(WrappedColumn)))
                    : null;
            }
        }
    }

    /// <summary>Writes the row of the shift sealed for the domain, unless the domain holds one already.</summary>
    internal static async Task InsertAsync(ContentTransaction transaction, int domainId, (SealedContent Shift, ContentKey Key) row, CancellationToken cancellationToken)
    {
        var command = transaction.CreateCommand(ContentQueries.InsertDateShift);
        await using (command.ConfigureAwait(false))
        {
            _ = command.Parameters.Add(new NpgsqlParameter("domain", NpgsqlDbType.Integer) { Value = domainId });
            _ = command.Parameters.Add(new NpgsqlParameter("shift", NpgsqlDbType.Bytea) { Value = row.Shift.Envelope.ToArray() });
            _ = command.Parameters.Add(new NpgsqlParameter("epoch", NpgsqlDbType.Integer) { Value = row.Key.Epoch });
            _ = command.Parameters.Add(new NpgsqlParameter("wrapped", NpgsqlDbType.Bytea) { Value = row.Key.Wrapped.ToArray() });
            _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>The shift as the row holds it, sealed, and the key of the row.</summary>
    internal sealed record SealedShift(byte[] Envelope, ContentKey Key);
}
