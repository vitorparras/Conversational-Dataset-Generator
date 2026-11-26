using Core.Ingestion.Pseudonymizing;

namespace Core.Host.Submissions;

/// <summary>
/// The draws of the markers of a submission (issue 102, F3): the conversation is swapped in one draw, and each item of another form is a conversation of its own, with no marker of an
/// item before it. The markers are held for the batch before the first item is written, drawn again without the ones another batch holds.
/// </summary>
internal static class SubmissionDraws
{
    /// <summary>The draws a conversation has before its writing fails, each one without the markers the ones before it met in another batch.</summary>
    internal const int Draws = 8;

    /// <summary>Holds the markers given for the batch, all or none, and names the ones another batch holds, none when the batch won them all.</summary>
    internal delegate Task<IReadOnlyList<string>> Reservation(string[] markers);

    /// <summary>The swap of the items, drawn again without the markers another batch holds until the batch holds every marker drawn, for as many draws as <see cref="Draws"/>.</summary>
    /// <param name="swap">Writes the markers.</param>
    /// <param name="items">The request of each item, and whether they are the messages of one conversation.</param>
    /// <param name="reserve">Holds the markers for the batch, all or none, and names the ones another batch holds.</param>
    internal static async Task<IReadOnlyList<Pseudonymized>> SwappedAsync(
        PseudonymSwap swap, (IReadOnlyList<SwapRequest> Requests, bool Threaded) items, Reservation reserve)
    {
        var lost = new HashSet<string>(StringComparer.Ordinal);
        for (var draw = 0; draw < Draws; draw++)
        {
            var swapped = Swapped(swap, items, lost.Contains);
            var taken = await reserve([.. swapped.SelectMany(item => item.Entries).Select(entry => entry.Marker.Text).Distinct(StringComparer.Ordinal)]).ConfigureAwait(false);
            if (taken.Count == 0)
            {
                return swapped;
            }

            lost.UnionWith(taken);
        }

        throw new InvalidOperationException("Every draw of the conversation met a marker another batch holds.");
    }

    private static IReadOnlyList<Pseudonymized> Swapped(PseudonymSwap swap, (IReadOnlyList<SwapRequest> Requests, bool Threaded) items, Func<string, bool> lost)
    {
        if (items.Threaded)
        {
            return swap.SwapConversation(items.Requests, marker => lost(marker.Text));
        }

        var given = new HashSet<string>(StringComparer.Ordinal);
        var swapped = new List<Pseudonymized>(items.Requests.Count);
        foreach (var request in items.Requests)
        {
            var item = swap.Swap(request, marker => lost(marker.Text) || given.Contains(marker.Text));
            given.UnionWith(item.Entries.Select(entry => entry.Marker.Text));
            swapped.Add(item);
        }

        return swapped;
    }
}
