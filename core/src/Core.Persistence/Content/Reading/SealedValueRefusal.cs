using Core.Persistence.Content.Sealing;
using Core.Persistence.Custody;
using Npgsql;

namespace Core.Persistence.Content.Reading;

/// <summary>
/// What a reading of a sealed value is told before anything is recorded, written once for every reader of protected content:
/// the loss of the key it is sealed under, which was declared, or a label the installation does not hold.
/// </summary>
internal static class SealedValueRefusal
{
    /// <summary>The refusal for the value, or none when it can be opened.</summary>
    internal static async Task<ContentReadResult?> OfAsync(
        (NpgsqlDataSource DataSource, ProtectedContent Protected) around, (byte[] Envelope, ContentKey Key) sealedValue, CancellationToken cancellationToken)
    {
        var lost = await DeclaredKeyLoss.ScopesAsync(around.DataSource, cancellationToken).ConfigureAwait(false);
        var carried = ProtectedContent.LabelsCarried(sealedValue.Envelope, sealedValue.Key);
        var scope = lost.Contains(KeyLossScope.MasterText) ? KeyLossScope.MasterText : carried.FirstOrDefault(lost.Contains);
        if (scope is not null)
        {
            return new ContentReadResult { Outcome = ContentReadOutcome.KeyLossDeclared, Label = scope };
        }

        var missing = carried.FirstOrDefault(label => !around.Protected.IsLoaded(label));
        return missing is null ? null : new ContentReadResult { Outcome = ContentReadOutcome.LabelMissing, Label = missing };
    }
}
