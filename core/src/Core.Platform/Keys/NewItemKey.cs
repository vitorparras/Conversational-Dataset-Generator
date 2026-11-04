namespace Core.Platform.Keys;

/// <summary>A key of a row just drawn, with the bytes that keep it in the database: the key wrapped under the label and the epoch, and the number of the epoch. The owner releases the key.</summary>
public sealed class NewItemKey : IDisposable
{
    internal NewItemKey()
    {
    }

    /// <summary>The key, to seal the content of the row with.</summary>
    public required ItemKey Key { get; init; }

    /// <summary>The epoch whose secret wrapped the key.</summary>
    public required int Epoch { get; init; }

    /// <summary>The key wrapped, which is what the row stores.</summary>
    public required ReadOnlyMemory<byte> Wrapped { get; init; }

    /// <summary>Releases the key.</summary>
    public void Dispose() => Key.Dispose();
}
