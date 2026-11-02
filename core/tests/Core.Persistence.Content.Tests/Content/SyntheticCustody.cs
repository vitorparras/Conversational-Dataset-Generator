using Core.Persistence.Content;
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
        Content = new ProtectedContent(labels, Keys);
    }

    public EnvelopeCipher Cipher { get; }

    public ItemKeys Keys { get; }

    public ProtectedContent Content { get; }

    /// <summary>The custody holding the labels and the epoch 1.</summary>
    public static SyntheticCustody Holding(params IEnumerable<string> labels) => Holding(1, labels);

    /// <summary>The custody holding the labels and the epochs of the numbers given, whose secrets are a function of the number.</summary>
    public static SyntheticCustody Holding(int epoch, IEnumerable<string> labels) => HoldingEpochs([epoch], labels);

    /// <summary>The custody holding the labels and every epoch of the numbers given.</summary>
    public static SyntheticCustody HoldingEpochs(IEnumerable<int> epochs, IEnumerable<string> labels)
    {
        var set = DerivationLabels.Of(labels);
        var key = Enumerable.Range(0, MasterKey.Length).Select(index => (byte)(0x5A ^ ((index * 29) + 3))).ToArray();
        var secrets = epochs.ToDictionary(number => number, number => SecretOf(number));
        return new SyntheticCustody(MasterKeyReading.FromBytes(key, set, CustodyEpochs.Of(secrets), "synthetic"), set);
    }

    /// <summary>The secret of the synthetic epoch of the number, the same whenever it is asked.</summary>
    public static byte[] SecretOf(int epoch) => [.. Enumerable.Range(0, MasterKey.Length).Select(index => (byte)(0xA7 ^ ((index * 31) + (epoch * 11))))];

    /// <summary>The text sealed for the column in a row of its own, with the key the row would store.</summary>
    public SealedRow Seal(ProtectedColumn column, int domainId, string text)
    {
        using var sealing = Content.Begin(column.Purpose == LabelPurpose.ReversalMap ? ContentColumns.MapKey : ContentColumns.ItemKey, domainId);
        return new SealedRow(sealing.Seal(column, text), sealing.Stored);
    }

    public void Dispose() => reading.Dispose();
}
