using Core.Platform.Keys;

namespace Core.Ingestion.Tests;

/// <summary>
/// The digest of a text does not depend on the epoch of the cipher or on the version of the label of the domain (issue 82, decision 0049): the master key and the purpose are all it is made of, so that
/// a rotation does not break the comparison with the occurrences already written.
/// </summary>
public sealed class ReplicationRotationTests
{
    private static readonly byte[] Secret = Enumerable.Repeat((byte)3, MasterKey.Length).ToArray();

    [Fact]
    public void TheDigestIsTheSameBeforeAndAfterTheRotationOfTheEpochAndOfTheLabelsOfTheDomain()
    {
        using var before = Reading(["texto-bruto/7/v1", "mapa-reversao/7/v1"], new Dictionary<int, byte[]> { [1] = Enumerable.Repeat((byte)9, 32).ToArray() });
        using var after = Reading(
            ["texto-bruto/7/v1", "mapa-reversao/7/v1", "texto-bruto/7/v2", "mapa-reversao/7/v2"],
            new Dictionary<int, byte[]> { [1] = Enumerable.Repeat((byte)9, 32).ToArray(), [2] = Enumerable.Repeat((byte)11, 32).ToArray() });

        var digestBefore = ReplicationDigests.OfText(new KeyDerivation(before.Key!, before.Labels), 7, "ZQX-ordem repassada");
        var digestAfter = ReplicationDigests.OfText(new KeyDerivation(after.Key!, after.Labels), 7, "ZQX-ordem repassada");
        var senderBefore = ReplicationDigests.OfSender(new KeyDerivation(before.Key!, before.Labels), 7, "ZQX-ana");
        var senderAfter = ReplicationDigests.OfSender(new KeyDerivation(after.Key!, after.Labels), 7, "ZQX-ana");

        Assert.Equal((digestBefore, senderBefore), (digestAfter, senderAfter));
    }

    [Fact]
    public void TheDigestDoesNotNeedAnyLabelToBeLoaded()
    {
        using var bare = Reading([], new Dictionary<int, byte[]>());
        using var full = Reading(["texto-bruto/7/v1"], new Dictionary<int, byte[]> { [1] = Enumerable.Repeat((byte)9, 32).ToArray() });

        Assert.Equal(
            ReplicationDigests.OfText(new KeyDerivation(bare.Key!, bare.Labels), 7, "ZQX-texto"),
            ReplicationDigests.OfText(new KeyDerivation(full.Key!, full.Labels), 7, "ZQX-texto"));
    }

    private static MasterKeyReading Reading(IEnumerable<string> labels, IReadOnlyDictionary<int, byte[]> epochs) =>
        MasterKeyReading.FromBytes(Secret.ToArray(), DerivationLabels.Of(labels), CustodyEpochs.Of(epochs), "synthetic");
}
