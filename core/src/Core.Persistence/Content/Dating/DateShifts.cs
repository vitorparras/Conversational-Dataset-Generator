using Core.Ingestion.Contracts.Dating;
using Core.Persistence.Content.Reading;
using Core.Persistence.Content.Sealing;
using Core.Persistence.Custody;
using Core.Persistence.Custody.Chain;
using Core.Platform.Keys;
using Npgsql;

namespace Core.Persistence.Content.Dating;

/// <summary>
/// The shift of the dates of each domain, kept in a row of its own, sealed with the purpose of the shift and the key of the row (decision 0020). What the row comes to is the
/// rule's (<see cref="DateShiftRule"/>): this only reads the row of the domain, opens it, and keeps the shift the rule drew.
/// </summary>
public sealed class DateShifts(NpgsqlDataSource dataSource, ProtectedContent protectedContent)
{
    /// <summary>
    /// The shift of the domain, drawn and kept the first time and read every time after; when two draw at once the second loses to the first and reads it. A row that does not open
    /// answers the loss, and nothing is drawn over it.
    /// </summary>
    /// <param name="domainId">The domain whose shift it is.</param>
    /// <param name="cancellationToken">Cancels the reading and the keeping.</param>
    public async Task<DateShiftReading> EstablishAsync(int domainId, CancellationToken cancellationToken)
    {
        var transaction = await ContentTransaction.BeginAsync(dataSource, domainId, cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            var reading = DateShiftRule.Establish(await StoredAsync(transaction, domainId, cancellationToken).ConfigureAwait(false));
            if (reading.Outcome == DateShiftOutcome.Drawn)
            {
                await KeepAsync(transaction, domainId, reading.Shift!, cancellationToken).ConfigureAwait(false);
                reading = DateShiftRule.Establish(await StoredAsync(transaction, domainId, cancellationToken).ConfigureAwait(false));
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return reading;
        }
    }

    private async Task KeepAsync(ContentTransaction transaction, int domainId, DateShift drawn, CancellationToken cancellationToken)
    {
        using var sealing = protectedContent.Begin(ContentColumns.DateShiftKey, domainId);
        try
        {
            await DateShiftStatements.InsertAsync(transaction, domainId, (sealing.Seal(ContentColumns.DateShift, drawn.Stored), sealing.Stored), cancellationToken).ConfigureAwait(false);
        }
        catch (PostgresException refused) when (refused.SqlState == CustodyStatements.ChainBrokenSqlState)
        {
            throw new CustodyChainBrokenException();
        }
    }

    /// <summary>The row of the domain: none, one that does not open (its loss declared, its label or its epoch not loaded, or bytes that do not authenticate), or what it opens to.</summary>
    private async Task<StoredDateShift> StoredAsync(ContentTransaction transaction, int domainId, CancellationToken cancellationToken)
    {
        if (await DateShiftStatements.ReadAsync(transaction, cancellationToken).ConfigureAwait(false) is not { } row)
        {
            return StoredDateShift.None;
        }

        try
        {
            return await SealedValueRefusal.OfAsync((dataSource, protectedContent), (row.Envelope, row.Key), cancellationToken).ConfigureAwait(false) is null
                ? StoredDateShift.Opened(protectedContent.Open(ContentColumns.DateShift, domainId, row.Envelope, row.Key))
                : StoredDateShift.Unopened;
        }
        catch (EnvelopeRefusedException)
        {
            return StoredDateShift.Unopened;
        }
    }
}
