namespace Core.Platform.Keys;

/// <summary>Where the master key comes from at startup, outside what was distributed; the port the means of delivery implements.</summary>
public interface IMasterKeySource
{
    /// <summary>Where the key is expected, as the operator reads it; never the key.</summary>
    string Origin { get; }

    /// <summary>Reads the key and the labels once; a key that is missing or cannot be read is a reading, not an exception.</summary>
    Task<MasterKeyReading> ReadAsync(CancellationToken cancellationToken);
}
