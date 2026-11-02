using Core.Platform.Keys;

namespace Core.Platform.Tests.Keys;

/// <summary>The epochs are held in memory only, shown by nothing, zeroed when released, and the ones in use that are missing are told by number.</summary>
public sealed class CustodyEpochsTests
{
    [Fact]
    public void EpochsAreKeptInOrderAndTheNewestIsTheGreatestNumber()
    {
        using var epochs = Of(3, 1, 2);

        Assert.Equal<IEnumerable<int>>([1, 2, 3], epochs.Numbers());
        Assert.Equal((3, true, false), (epochs.Newest, epochs.Holds(2), epochs.Holds(4)));
    }

    [Fact]
    public void MissingEpochsAmongThoseInUseAreToldOnceAndInOrder()
    {
        using var epochs = Of(2);

        Assert.Equal<IEnumerable<int>>([1, 3, 5], epochs.Lacking([5, 3, 2, 1, 3]));
    }

    [Fact]
    public void SetWithNoEpochHasNoNewestAndNothingToGive()
    {
        var none = CustodyEpochs.None;

        Assert.Equal(("No epoch is loaded.", true), (Assert.Throws<InvalidOperationException>(() => none.Newest).Message, none.Numbers().ToArray().Length == 0));
    }

    [Fact]
    public void SecretsGivenAreZeroedHereSoNoOtherCopyStaysBehind()
    {
        var secret = Enumerable.Repeat((byte)7, MasterKey.Length).ToArray();

        using var epochs = CustodyEpochs.Of(new Dictionary<int, byte[]> { [1] = secret });

        Assert.All(secret, value => Assert.Equal(0, value));
    }

    [Fact]
    public void ReleasedEpochsGiveNoSecretAndTheTextShowsNothing()
    {
        var epochs = Of(1);
        var shown = epochs.ToString();
        epochs.Dispose();

        Assert.Equal(("CustodyEpochs", true), (shown, Assert.Throws<ObjectDisposedException>(() => epochs.SecretOf(1).Length) is not null));
    }

    [Fact]
    public void EpochOfANumberBelowOneOrOfASecretOfAnotherSizeIsADefectOfTheCaller()
    {
        var right = new byte[MasterKey.Length];
        var wrong = new byte[MasterKey.Length - 1];

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => CustodyEpochs.Of(new Dictionary<int, byte[]> { [0] = right }));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => CustodyEpochs.Of(new Dictionary<int, byte[]> { [1] = wrong }));
    }

    [Fact]
    public void LackingAndTheSetOfNothingAreRefusedByNameWhenNull()
    {
        using var epochs = Of(1);

        Assert.Equal(
            ["used", "given"],
            [
                Assert.Throws<ArgumentNullException>(() => epochs.Lacking(null!)).ParamName,
                Assert.Throws<ArgumentNullException>(() => CustodyEpochs.Of(null!)).ParamName,
            ]);
    }

    private static CustodyEpochs Of(params int[] numbers) =>
        CustodyEpochs.Of(numbers.ToDictionary(number => number, number => Enumerable.Repeat((byte)number, MasterKey.Length).ToArray()));
}
