namespace Core.Persistence.Content.Reading;

/// <summary>What reading the occurrences of an item gave: how the reading ended and, only when it was read, the occurrences as they were decided when the item came in.</summary>
/// <param name="Reading">How the reading ended, as for any protected column; a decision was never made for an item whose reading is <see cref="ContentReadOutcome.Empty" />.</param>
/// <param name="Occurrences">
/// The occurrences with the destination chosen for each, in the order they were found; empty when the text was analysed and nothing was found;
/// null unless the reading is <see cref="ContentReadOutcome.Read" />.
/// </param>
public sealed record ItemOccurrencesRead(ContentReadResult Reading, IReadOnlyList<ItemOccurrence>? Occurrences);
