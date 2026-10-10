namespace Core.Platform.Secrets;

/// <summary>
/// Where the master key comes from: supplied to the process at startup, from outside the artifact.
/// A port only; the custody that implements it is decision 0014's, and arrives with the first
/// module that encrypts.
/// </summary>
public interface IMasterKeySource
{
    /// <summary>Where the key is read from, as the operator names it in a startup refusal.</summary>
    string Origin { get; }

    /// <summary>Reads the key. The caller owns the bytes and clears them after use.</summary>
    ValueTask<byte[]> ReadAsync(CancellationToken cancellationToken);
}
