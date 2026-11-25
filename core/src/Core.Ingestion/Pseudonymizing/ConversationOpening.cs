namespace Core.Ingestion.Pseudonymizing;

/// <summary>
/// Opens the markers of the conversation of one item (decision 0057). The composition root gives it the draw, so that the swap never builds the markers itself.
/// </summary>
/// <param name="inUseElsewhere">Whether a marker is held by another conversation, of any domain.</param>
/// <param name="known">The markers the item already has, by referent.</param>
/// <returns>The markers of the conversation.</returns>
public delegate ConversationMarkers ConversationOpening(Func<Marker, bool> inUseElsewhere, IReadOnlyDictionary<Referent, Marker> known);
