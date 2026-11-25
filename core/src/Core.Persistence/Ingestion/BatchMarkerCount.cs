namespace Core.Persistence.Ingestion;

/// <summary>How many markers of one kind the items of a batch hold, and how many items hold one: numbers alone, never the marker.</summary>
/// <param name="Kind">The kind: <c>person</c>, <c>place</c>, <c>organization</c>, or <c>author</c> for the markers that stand for the author of a message.</param>
/// <param name="Markers">How many distinct markers of the kind.</param>
/// <param name="Items">How many items hold at least one of them.</param>
public sealed record BatchMarkerCount(string Kind, long Markers, long Items);
