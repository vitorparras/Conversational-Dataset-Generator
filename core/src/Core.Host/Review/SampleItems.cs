using Core.Host.Screens;
using Core.Persistence.Content.Reading;
using Core.Persistence.Ingestion;

namespace Core.Host.Review;

/// <summary>
/// The items of the sample as the screen shows them. While the batch is open each text is read through the door of the sample, which records the reading of each one;
/// once the batch is decided no text is read again, and the items say so.
/// </summary>
internal static class SampleItems
{
    private const string Decided = "O texto deste item não é mais mostrado: o lote já foi decidido.";

    /// <summary>The items of the open batch, one reading each, in the order of the draw; null when any text could not be read, since a sample shown in part would be reviewed in part.</summary>
    internal static async Task<List<SampleReviewScreen.Item>?> OpenAsync(SampleTextReader reader, (long Batch, int Domain, long Reader) at, BatchReview held, CancellationToken cancellationToken)
    {
        var items = new List<SampleReviewScreen.Item>();
        foreach (var (id, index) in held.Sample!.Select((id, index) => (id, index)))
        {
            var read = await reader.ReadAsync(new SampleReading(at.Domain, at.Batch, id, at.Reader), cancellationToken).ConfigureAwait(false);
            if (read.Outcome != ContentReadOutcome.Read)
            {
                return null;
            }

            items.Add(new SampleReviewScreen.Item(index + 1, read.Text!, read.Cleaned, index + 1 <= held.ReviewedPosition));
        }

        return items;
    }

    /// <summary>The items of a batch already decided: their places and how far the review went, and none of the text.</summary>
    internal static List<SampleReviewScreen.Item> Closed(BatchReview held) =>
        [.. held.Sample!.Select((_, index) => new SampleReviewScreen.Item(index + 1, Decided, Cleaned: false, held.State == BatchState.Accepted || index + 1 <= held.ReviewedPosition))];
}
