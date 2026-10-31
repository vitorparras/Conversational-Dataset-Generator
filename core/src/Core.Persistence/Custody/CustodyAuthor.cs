namespace Core.Persistence.Custody;

/// <summary>Who did what the event records: an identified person, or the system itself. Nobody is left blank, and the system signs as the system.</summary>
public sealed record CustodyAuthor
{
    private CustodyAuthor(long? personId)
    {
        PersonId = personId;
    }

    /// <summary>The system acting on its own.</summary>
    public static CustodyAuthor System { get; } = new(personId: null);

    /// <summary>The identifier of the person; null when the author is the system.</summary>
    public long? PersonId { get; }

    /// <summary>A person of the registry, who must be active when the event is written.</summary>
    /// <param name="personId">The identifier of the person.</param>
    public static CustodyAuthor Person(long personId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(personId);
        return new CustodyAuthor(personId);
    }
}
