using System.Security.Cryptography;

namespace Core.Ingestion.Pseudonymizing;

/// <summary>The draw of the number of a marker, by the cryptographic generator, again and again until the marker is not one in use.</summary>
public sealed class MarkerDraw
{
    private readonly Func<int> next;

    /// <summary>The draw by the cryptographic generator.</summary>
    public MarkerDraw()
        : this(() => RandomNumberGenerator.GetInt32(Marker.Numbers))
    {
    }

    /// <summary>The draw with the numbers given, so that a test can make a draw repeat a number in use.</summary>
    /// <param name="next">Gives the next number, from zero to <see cref="Marker.Numbers" /> less one.</param>
    internal MarkerDraw(Func<int> next)
    {
        ArgumentNullException.ThrowIfNull(next);
        this.next = next;
    }

    /// <summary>A marker of the kind that is not in use, drawn again for as long as the one drawn is.</summary>
    /// <param name="kind">What the marker stands for.</param>
    /// <param name="inUse">Whether a marker is in use, in this conversation or in any other.</param>
    public Marker Draw(MarkerKind kind, Func<Marker, bool> inUse)
    {
        ArgumentNullException.ThrowIfNull(inUse);
        var marker = new Marker(kind, next());
        while (inUse(marker))
        {
            marker = new Marker(kind, next());
        }

        return marker;
    }
}
