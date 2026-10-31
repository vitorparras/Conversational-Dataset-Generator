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
        Content = new ProtectedContent(new EnvelopeCipher(new KeyDerivation(reading.Key!, labels)), labels);
    }

    public ProtectedContent Content { get; }

    public static SyntheticCustody Holding(params IEnumerable<string> labels)
    {
        var set = DerivationLabels.Of(labels);
        var key = Enumerable.Range(0, MasterKey.Length).Select(index => (byte)(0x5A ^ ((index * 29) + 3))).ToArray();
        return new SyntheticCustody(MasterKeyReading.FromBytes(key, set), set);
    }

    public void Dispose() => reading.Dispose();
}
