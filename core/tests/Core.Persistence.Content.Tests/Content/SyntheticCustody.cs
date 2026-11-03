using Core.Persistence.Content;
using Core.Platform.Keys;

namespace Core.Persistence.Tests.Content;

/// <summary>A master key that belongs to no installation, with the labels given, and the path of protected content built over them.</summary>
internal sealed class SyntheticCustody : IDisposable
{
    private readonly MasterKeyReading reading;

    private SyntheticCustody(MasterKeyReading reading, DerivationLabels labels)
    {
        this.reading = reading;
        Cipher = new EnvelopeCipher(new KeyDerivation(reading.Key!, labels));
        Content = new ProtectedContent(Cipher, labels);
    }

    public EnvelopeCipher Cipher { get; }

    public ProtectedContent Content { get; }

    /// <summary>The bytes of the synthetic key, a copy each time: the reading that holds the key wipes the one it receives.</summary>
    public static byte[] KeyBytes() => [.. Enumerable.Range(0, MasterKey.Length).Select(index => (byte)(0x5A ^ ((index * 29) + 3)))];

    public static SyntheticCustody Holding(params IEnumerable<string> labels)
    {
        var set = DerivationLabels.Of(labels);
        return new SyntheticCustody(MasterKeyReading.FromBytes(KeyBytes(), set), set);
    }

    public void Dispose() => reading.Dispose();
}
