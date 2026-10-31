using System.Security.Cryptography;

namespace Core.Platform.Keys;

/// <summary>What a source gave: the key with the labels that travel with it, or the reason there is no key; the owner releases it.</summary>
public sealed class MasterKeyReading : IDisposable
{
    private MasterKeyReading()
    {
    }

    /// <summary>The key; null when the source gave none.</summary>
    public MasterKey? Key { get; private init; }

    /// <summary>The labels the source gave; none when it gave no key.</summary>
    public DerivationLabels Labels { get; private init; } = DerivationLabels.None;

    /// <summary>Why there is no key; null when there is one.</summary>
    public MasterKeyFailure? Failure { get; private init; }

    /// <summary>The key of the bytes given, which are zeroed here so that no other copy stays behind; a size other than the key's is a failure that says the size found.</summary>
    public static MasterKeyReading FromBytes(byte[] bytes, DerivationLabels labels) => FromBytes(bytes, labels, source: string.Empty);

    /// <summary>As <see cref="FromBytes(byte[], DerivationLabels)"/>, a failure of size naming <paramref name="source"/>, where the bytes came from.</summary>
    public static MasterKeyReading FromBytes(byte[] bytes, DerivationLabels labels, string source)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        ArgumentNullException.ThrowIfNull(labels);
        ArgumentNullException.ThrowIfNull(source);
        var origin = source.Length == 0 ? string.Empty : source + ": ";
        try
        {
            return bytes.Length == MasterKey.Length
                ? new MasterKeyReading { Key = new MasterKey(bytes), Labels = labels }
                : Failed(MasterKeyFailureKind.WrongSize, $"{origin}{bytes.Length} bytes, esperados {MasterKey.Length}");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    /// <summary>Nothing was provided; <paramref name="missing"/> says what.</summary>
    public static MasterKeyReading Absent(string missing) => Failed(MasterKeyFailureKind.Absent, missing);

    /// <summary>Something was provided and could not be read as a key; <paramref name="found"/> says what it was, never its value.</summary>
    public static MasterKeyReading Unreadable(string found) => Failed(MasterKeyFailureKind.Unreadable, found);

    /// <summary>Releases the key, if any.</summary>
    public void Dispose() => Key?.Dispose();

    private static MasterKeyReading Failed(MasterKeyFailureKind kind, string found)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(found);
        return new MasterKeyReading { Failure = new MasterKeyFailure(kind, found) };
    }
}
