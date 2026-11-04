using Core.Persistence.Custody;
using Core.Persistence.Custody.Chain;
using Npgsql;
using NpgsqlTypes;

namespace Core.Persistence.Recovery;

/// <summary>
/// The record of the rehearsal in the chain of the production: the receipt is read, its top of the copy has to be an event of this chain with that very digest, and its proof has to be that of the key
/// of the custody here. Only then is the step written, and when either fails nothing is written and the answer says which.
/// </summary>
public sealed class RecoveryRecording
{
    private const string TopInChain = "SELECT EXISTS (SELECT 1 FROM public.custody_event WHERE sequence = @sequence AND encode(digest, 'hex') = @digest)";

    private readonly NpgsqlDataSource dataSource;

    /// <summary>The record reads and writes through the data source built with the credential of the owner.</summary>
    public RecoveryRecording(NpgsqlDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        this.dataSource = dataSource;
    }

    /// <summary>Records the rehearsal the receipt tells, if the chain and the key here confirm it.</summary>
    /// <param name="receiptText">The text of the receipt, as the rehearsal printed it.</param>
    /// <param name="keyProof">The proof of the key of the custody here, as <see cref="KeyProof.Of" /> gives it.</param>
    /// <param name="cancellationToken">Cancels the record.</param>
    public async Task<RecoveryRecord> RecordAsync(string? receiptText, string keyProof, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(keyProof);
        var reading = RecoveryReceipt.Read(receiptText);
        if (reading.Receipt is not { } receipt)
        {
            return RecoveryRecord.Refused(RecoveryOutcome.ReceiptRefused, reading.Refusal!);
        }

        var failures = new List<string>();
        if (!await HoldsTopAsync(receipt, cancellationToken).ConfigureAwait(false))
        {
            failures.Add("o topo da cópia não está na cadeia da produção");
        }

        if (!string.Equals(receipt.KeyProof, keyProof, StringComparison.Ordinal))
        {
            failures.Add("a chave do ensaio não é a do arquivo de custódia daqui");
        }

        if (!receipt.Counts.Holds)
        {
            failures.Add("o ensaio teve amostras que não abriram, ou nenhuma abriu");
        }

        return failures.Count > 0 ? RecoveryRecord.Refused(RecoveryOutcome.NotConfirmed, failures) : await WriteAsync(receipt, cancellationToken).ConfigureAwait(false);
    }

    private async Task<RecoveryRecord> WriteAsync(RecoveryReceipt receipt, CancellationToken cancellationToken)
    {
        var chain = await new CustodyChain(dataSource).AppendAsync(CustodyEvent.RecoveryRehearsed(receipt.TopSequence), cancellationToken).ConfigureAwait(false);
        return chain.Outcome == CustodyOutcome.Appended
            ? RecoveryRecord.Written(chain.Sequence ?? 0)
            : RecoveryRecord.Refused(RecoveryOutcome.ChainRefused, $"a cadeia não aceitou o evento ({chain.Outcome})");
    }

    private async Task<bool> HoldsTopAsync(RecoveryReceipt receipt, CancellationToken cancellationToken)
    {
        var command = dataSource.CreateCommand(TopInChain);
        await using (command.ConfigureAwait(false))
        {
            _ = command.Parameters.AddWithValue("sequence", NpgsqlDbType.Bigint, receipt.TopSequence);
            _ = command.Parameters.AddWithValue("digest", NpgsqlDbType.Text, receipt.TopDigest);
            return (bool)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
        }
    }
}
