using System.Security.Cryptography;

namespace Core.Platform.Keys;

/// <summary>What a source gave: the key with the labels that travel with it, or the reason there is no key; the owner releases it.</summary>
public sealed class MasterKeyReading : IDisposable
{
    private MasterKeyReading(MasterKey? key, DerivationLabels labels, MasterKeyFailure? failure)
    {
        Key = key;
        Labels = labels;
        Failure = failure;
    }

    /// <summary>The key; null when the source gave none.</summary>
    public MasterKey? Key { get; }

    /// <summary>The labels the source gave; none when it gave no key.</summary>
    public DerivationLabels Labels { get; }

    /// <summary>Why there is no key; null when there is one.</summary>
    public MasterKeyFailure? Failure { get; }

    /// <summary>The key of the bytes given, which are zeroed here so that no other copy stays behind; a size other than the key's is a failure that says the size found.</summary>
    public static MasterKeyReading FromBytes(byte[] bytes, DerivationLabels labels)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        ArgumentNullException.ThrowIfNull(labels);
        try
        {
            return bytes.Length == MasterKey.Length
                ? new MasterKeyReading(new MasterKey(bytes), labels, failure: null)
                : Failed(MasterKeyFailureKind.WrongSize, $"{bytes.Length} bytes, esperados {MasterKey.Length}");
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
        return new MasterKeyReading(key: null, DerivationLabels.None, new MasterKeyFailure(kind, found));
    }
}
