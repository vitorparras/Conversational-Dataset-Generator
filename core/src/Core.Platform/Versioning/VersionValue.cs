namespace Core.Platform.Versioning;

/// <summary>
/// A version the system reports: either a value it obtained, or the explicit statement that it did
/// not obtain one. Never null and never empty, so "unknown" survives any reading (issue 51 CA-2).
/// </summary>
public abstract record VersionValue
{
    private protected VersionValue()
    {
    }

    /// <summary>The value when obtained.</summary>
    public static VersionValue Of(string value) => new Known(value);

    /// <summary>The value when obtained, or absent when <paramref name="value"/> is null or empty.</summary>
    public static VersionValue OfOrAbsent(string? value) =>
        string.IsNullOrWhiteSpace(value) ? Absent.Instance : new Known(value);
}

/// <summary>A version the system obtained.</summary>
public sealed record Known : VersionValue
{
    /// <summary>A non-empty version.</summary>
    public Known(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>The version as obtained.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>A version the system did not obtain. Not a value, not empty, not a guess.</summary>
public sealed record Absent : VersionValue
{
    private Absent()
    {
    }

    /// <summary>The single absent value.</summary>
    public static Absent Instance { get; } = new();

    /// <inheritdoc />
    public override string ToString() => "absent";
}
