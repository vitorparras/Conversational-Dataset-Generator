using System.Globalization;

namespace Core.Ingestion.Pseudonymizing;

/// <summary>
/// The mark written in place of a referent: the prefix of its kind and a number of eight digits that was drawn, never computed from the original (decision 0012), so that it
/// carries neither initial, nor length, nor alphabetical place, nor the order the names came in.
/// </summary>
public sealed record Marker
{
    private const int EightDigits = 100_000_000;

    /// <summary>The marker of the kind with the number drawn.</summary>
    /// <param name="kind">What the marker stands for.</param>
    /// <param name="number">The number drawn, from zero to <see cref="Numbers" /> less one.</param>
    public Marker(MarkerKind kind, int number)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "The kind of marker is not known.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(number);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(number, Numbers);
        Kind = kind;
        Number = number;
    }

    /// <summary>How many numbers a marker may have: eight digits.</summary>
    public static int Numbers => EightDigits;

    /// <summary>What the marker stands for.</summary>
    public MarkerKind Kind { get; }

    /// <summary>The number drawn.</summary>
    public int Number { get; }

    /// <summary>The marker as the pseudonymized text writes it, such as PESSOA_48291307.</summary>
    public string Text => string.Create(CultureInfo.InvariantCulture, $"{MarkerKinds.Prefix(Kind)}_{Number:D8}");

    /// <summary>The marker as written, which says nothing of the original.</summary>
    public override string ToString() => Text;
}
