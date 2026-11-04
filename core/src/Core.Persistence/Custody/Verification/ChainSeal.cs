namespace Core.Persistence.Custody.Verification;

/// <summary>
/// The seal of the chain: the moment it was made, how many events the chain had and the digest of the last of them, and nothing of the content. It is what someone keeps outside the
/// control of the database, to compare the chain with later. It has a form in text, versioned and checked, to print or copy.
/// </summary>
public sealed record ChainSeal
{
    private ChainSeal(DateTimeOffset sealedAt, long events, string digest)
    {
        SealedAt = sealedAt;
        Events = events;
        Digest = digest;
    }

    /// <summary>The moment of the seal, in UTC and in whole seconds.</summary>
    public DateTimeOffset SealedAt { get; }

    /// <summary>How many events the chain had, which is also the sequence of the last of them; 0 for a chain with none.</summary>
    public long Events { get; }

    /// <summary>The digest of the last event sealed, in 64 lowercase hexadecimal digits; for a chain with none, the origin of the chain.</summary>
    public string Digest { get; }

    /// <summary>A seal of a chain with the events and the last digest given.</summary>
    /// <param name="sealedAt">The moment of the seal; the fraction of a second is dropped.</param>
    /// <param name="events">How many events the chain had.</param>
    /// <param name="digest">The digest of the last event, 32 bytes.</param>
    public static ChainSeal Of(DateTimeOffset sealedAt, long events, ReadOnlySpan<byte> digest)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(events);
        ArgumentOutOfRangeException.ThrowIfNotEqual(digest.Length, ChainDigest.Length);
        var utc = sealedAt.ToUniversalTime();
        return new ChainSeal(new DateTimeOffset(utc.Ticks - (utc.Ticks % TimeSpan.TicksPerSecond), TimeSpan.Zero), events, Convert.ToHexStringLower(digest));
    }

    /// <summary>Reads a seal back from its form in text; a text that was altered, cut short or is of another form is not read, and the answer says why.</summary>
    /// <param name="text">The text <see cref="ToText" /> gave; blanks around it are ignored.</param>
    public static SealReading Read(string? text) => SealText.Read(text);

    /// <summary>The form in text: one line, the version, the moment, the count, the digest and a check code, apart by a vertical bar.</summary>
    public string ToText() => SealText.Format(this);
}
