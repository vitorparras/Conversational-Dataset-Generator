namespace Core.Platform.Keys;

/// <summary>A key of a row just drawn, with the bytes that keep it in the database: the key wrapped under the label and the epoch, and the number of the epoch. The owner releases the key.</summary>
public sealed class NewItemKey : IDisposable
{
    internal NewItemKey(ItemKey key, int epoch, ReadOnlyMemory<byte> wrapped)
    {
        Key = key;
        Epoch = epoch;
        Wrapped = wrapped;
    }

    /// <summary>The key, to seal the content of the row with.</summary>
    public ItemKey Key { get; }

    /// <summary>The epoch whose secret wrapped the key.</summary>
    public int Epoch { get; }

    /// <summary>The key wrapped, which is what the row stores.</summary>
    public ReadOnlyMemory<byte> Wrapped { get; }

    /// <summary>Releases the key.</summary>
    public void Dispose() => Key.Dispose();
}
