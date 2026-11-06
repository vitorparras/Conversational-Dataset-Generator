namespace Core.Ingestion;

/// <summary>The neutral structure a reader hands over for one item: the units it read, in the order the content gives them, and nothing about the file they came from.</summary>
public sealed record ReadMaterial
{
    /// <summary>The material holds the units read.</summary>
    /// <param name="units">The units, as the content presents them.</param>
    public ReadMaterial(IReadOnlyList<ReadUnit> units)
    {
        ArgumentNullException.ThrowIfNull(units);
        Units = units;
    }

    /// <summary>The units read.</summary>
    public IReadOnlyList<ReadUnit> Units { get; }
}
