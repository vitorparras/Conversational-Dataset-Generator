namespace Core.Ingestion.Pseudonymizing;

/// <summary>
/// The markers of one conversation, its scope (decision 0057): a referent gets the marker it already has here, or a drawn one that neither this conversation nor any other
/// holds. Nothing here reads the original to choose the marker, so the same name in two conversations or two domains gets two markers with nothing relating them.
/// </summary>
public sealed class ConversationMarkers
{
    private readonly MarkerDraw draw;
    private readonly Func<Marker, bool> inUseElsewhere;
    private readonly Dictionary<Referent, Marker> assigned;

    /// <summary>The markers of a conversation pseudonymized for the first time, which has none yet.</summary>
    /// <param name="draw">Draws the number of a new marker.</param>
    /// <param name="inUseElsewhere">Whether a marker is held by another conversation, of any domain.</param>
    public ConversationMarkers(MarkerDraw draw, Func<Marker, bool> inUseElsewhere)
        : this(draw, inUseElsewhere, new Dictionary<Referent, Marker>())
    {
    }

    /// <summary>The markers of a conversation that keeps the ones its item already has.</summary>
    /// <param name="draw">Draws the number of a new marker.</param>
    /// <param name="inUseElsewhere">Whether a marker is held by another conversation, of any domain.</param>
    /// <param name="known">The markers the item already has, by referent.</param>
    public ConversationMarkers(MarkerDraw draw, Func<Marker, bool> inUseElsewhere, IReadOnlyDictionary<Referent, Marker> known)
    {
        ArgumentNullException.ThrowIfNull(draw);
        ArgumentNullException.ThrowIfNull(inUseElsewhere);
        ArgumentNullException.ThrowIfNull(known);
        this.draw = draw;
        this.inUseElsewhere = inUseElsewhere;
        assigned = new Dictionary<Referent, Marker>(known);
    }

    /// <summary>The markers given so far, by referent.</summary>
    public IReadOnlyDictionary<Referent, Marker> Assigned => assigned;

    /// <summary>The marker of the referent: the one it has in the conversation, or a new one of the kind given.</summary>
    /// <param name="referent">Who or what the mention refers to.</param>
    /// <param name="kind">What the mention names, used only when the referent has no marker yet.</param>
    public Marker Of(Referent referent, MarkerKind kind)
    {
        ArgumentNullException.ThrowIfNull(referent);
        if (assigned.TryGetValue(referent, out var marker))
        {
            return marker;
        }

        var held = assigned.Values.ToHashSet();
        marker = draw.Draw(kind, candidate => held.Contains(candidate) || inUseElsewhere(candidate));
        assigned.Add(referent, marker);
        return marker;
    }
}
