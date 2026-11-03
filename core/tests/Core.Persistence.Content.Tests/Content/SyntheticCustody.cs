using Core.Persistence.Content.Sealing;
using Core.Platform.Keys;

namespace Core.Persistence.Tests.Content;

/// <summary>A synthetic master key, labels and epochs, and the path of protected content over them.</summary>
internal sealed class SyntheticCustody : IDisposable
{
    private readonly MasterKeyReading reading;

    private SyntheticCustody(MasterKeyReading reading, DerivationLabels labels)
    {
        this.reading = reading;
        Cipher = new EnvelopeCipher(new KeyDerivation(reading.Key!, labels));
        Keys = new ItemKeys(new KeyDerivation(reading.Key!, labels), reading.Epochs);
        Labels = labels;
        Content = ContentRegistering(labels.All);
    }

    public EnvelopeCipher Cipher { get; }

    public ItemKeys Keys { get; }

    public ProtectedContent Content { get; }

    /// <summary>The labels the custody holds.</summary>
    public DerivationLabels Labels { get; }

    /// <summary>The bytes of the synthetic key, a copy each time: the reading that holds the key wipes the one it receives.</summary>
    public static byte[] KeyBytes() => [.. Enumerable.Range(0, MasterKey.Length).Select(index => (byte)(0x5A ^ ((index * 29) + 3)))];

    /// <summary>The custody holding the labels and the epoch 1.</summary>
    public static SyntheticCustody Holding(params IEnumerable<string> labels) => Holding(1, labels);

    /// <summary>The custody holding the labels and the epochs of the numbers given, whose secrets are a function of the number.</summary>
    public static SyntheticCustody Holding(int epoch, IEnumerable<string> labels) => HoldingEpochs([epoch], labels);

    /// <summary>The custody holding the labels and every epoch of the numbers given.</summary>
    public static SyntheticCustody HoldingEpochs(IEnumerable<int> epochs, IEnumerable<string> labels)
    {
        var set = DerivationLabels.Of(labels);
        var key = KeyBytes();
        var secrets = epochs.ToDictionary(number => number, number => SecretOf(number));
        return new SyntheticCustody(MasterKeyReading.FromBytes(key, set, CustodyEpochs.Of(secrets), "synthetic"), set);
    }

    /// <summary>The custody holding the labels and the epochs given as number and secret, such as an epoch number that holds the secret of another.</summary>
    public static SyntheticCustody HoldingSecrets(IReadOnlyDictionary<int, byte[]> epochs, IEnumerable<string> labels)
    {
        var set = DerivationLabels.Of(labels);
        var key = KeyBytes();
        return new SyntheticCustody(MasterKeyReading.FromBytes(key, set, CustodyEpochs.Of(epochs), "synthetic"), set);
    }

    /// <summary>The secret of the synthetic epoch of the number, the same whenever it is asked.</summary>
    public static byte[] SecretOf(int epoch) => [.. Enumerable.Range(0, MasterKey.Length).Select(index => (byte)(0xA7 ^ ((index * 31) + (epoch * 11))))];

    /// <summary>The path of protected content over this custody when the database registers only the labels given.</summary>
    public ProtectedContent ContentRegistering(IEnumerable<string> registered) => new(Labels, Keys, new HashSet<string>(registered, StringComparer.Ordinal));

    /// <summary>The text sealed for the column in a row of its own, with the key the row would store.</summary>
    public SealedRow Seal(ProtectedColumn column, int domainId, string text)
    {
        using var sealing = Content.Begin(column.Purpose == LabelPurpose.ReversalMap ? ContentColumns.MapKey : ContentColumns.ItemKey, domainId);
        return new SealedRow(sealing.Seal(column, text), sealing.Stored);
    }

    public void Dispose() => reading.Dispose();
}
