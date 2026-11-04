using Core.Platform.Keys;

namespace Core.Platform.Tests.Keys;

/// <summary>The key of a row is random, is kept wrapped under its label and an epoch, and cannot be unwrapped without that epoch (decision 0049).</summary>
public sealed class ItemKeysTests : IDisposable
{
    private const string Label = "texto-bruto/7/v1";
    private const string Next = "texto-bruto/7/v2";

    private readonly MasterKey master = new(SyntheticKey.Valid());
    private readonly CustodyEpochs both = Epochs(1, 2);
    private readonly CustodyEpochs onlyTheFirst = Epochs(1);
    private readonly CustodyEpochs onlyTheSecond = Epochs(2);

    public void Dispose()
    {
        master.Dispose();
        both.Dispose();
        onlyTheFirst.Dispose();
        onlyTheSecond.Dispose();
    }

    [Fact]
    public void CreatedKeyIsWrappedUnderTheNewestEpochAndUnwrapsToTheSameBytes()
    {
        using var created = Keys(both).Create(Label);

        using var unwrapped = Keys(both).Unwrap(created.Epoch, created.Wrapped.Span);

        Assert.Equal((2, ItemKey.Length, true), (created.Epoch, created.Key.AsSpan().Length, created.Key.AsSpan().SequenceEqual(unwrapped.AsSpan())));
    }

    [Fact]
    public void TwoKeysAreNeverTheSame()
    {
        var keys = Keys(both);
        using var first = keys.Create(Label);
        using var second = keys.Create(Label);

        Assert.False(first.Key.AsSpan().SequenceEqual(second.Key.AsSpan()));
    }

    [Fact]
    public void WrappedKeyCarriesTheLabelAndNotTheKey()
    {
        using var created = Keys(both).Create(Label);

        Assert.Equal((Label, false), (EnvelopeCipher.LabelOf(created.Wrapped.Span), created.Wrapped.Span.IndexOf(created.Key.AsSpan()) >= 0));
    }

    [Fact]
    public void WrappedKeyDoesNotUnwrapWhenItsEpochWasDestroyed()
    {
        using var created = Keys(onlyTheFirst).Create(Label);
        var wrapped = created.Wrapped.ToArray();

        var refused = Assert.Throws<EnvelopeRefusedException>(() => Keys(onlyTheSecond).Unwrap(created.Epoch, wrapped));

        Assert.Equal("The epoch 1 of the key is not among the loaded ones.", refused.Message);
    }

    [Fact]
    public void WrappedKeyDoesNotUnwrapUnderAnotherSecretWithTheSameNumber()
    {
        using var created = Keys(onlyTheFirst).Create(Label);
        var wrapped = created.Wrapped.ToArray();
        using var forged = Epochs((1, 2));

        _ = Assert.Throws<EnvelopeRefusedException>(() => Keys(forged).Unwrap(1, wrapped));
    }

    [Fact]
    public void WrappedKeyDoesNotUnwrapUnderTheNumberOfAnotherEpoch()
    {
        using var created = Keys(both).Create(Label);
        var wrapped = created.Wrapped.ToArray();

        _ = Assert.Throws<EnvelopeRefusedException>(() => Keys(both).Unwrap(1, wrapped));
    }

    [Fact]
    public void RewrappedKeyUnwrapsToTheSameKeyUnderTheNewEpochAlone()
    {
        using var created = Keys(onlyTheFirst).Create(Label);
        var wrapped = created.Wrapped.ToArray();

        var again = Keys(both).Rewrap(1, wrapped, 2);
        using var unwrapped = Keys(onlyTheSecond).Unwrap(2, again);

        Assert.Equal((true, false), (unwrapped.AsSpan().SequenceEqual(created.Key.AsSpan()), again.AsSpan().SequenceEqual(wrapped)));
    }

    [Fact]
    public void RewrappedKeyUnderANewLabelUnwrapsToTheSameKeyAndCarriesTheNewLabel()
    {
        using var created = Keys(both).Create(Label);
        var wrapped = created.Wrapped.ToArray();
        var keys = new ItemKeys(new KeyDerivation(master, DerivationLabels.Of([Label, Next])), both);

        var again = keys.Rewrap(created.Epoch, wrapped, created.Epoch, Next);
        using var unwrapped = keys.Unwrap(created.Epoch, again);

        Assert.Equal((Next, true), (EnvelopeCipher.LabelOf(again), unwrapped.AsSpan().SequenceEqual(created.Key.AsSpan())));
    }

    [Fact]
    public void CreatingWithoutAnyEpochIsRefused()
    {
        var keys = Keys(CustodyEpochs.None);

        _ = Assert.Throws<InvalidOperationException>(() => keys.Create(Label));
    }

    [Fact]
    public void ReleasedKeyHasNoBytesToGiveAndShowsNothing()
    {
        var created = Keys(both).Create(Label);
        var shown = created.Key.ToString();
        created.Dispose();

        Assert.Equal(("ItemKey", true), (shown, Assert.Throws<ObjectDisposedException>(() => created.Key.AsSpan().Length) is not null));
    }

    [Fact]
    public void NullAndBlankArgumentsAreRefusedByName()
    {
        var derivation = new KeyDerivation(master, DerivationLabels.Of([Label]));
        var keys = Keys(both);

        Assert.Equal(
            ["derivation", "epochs", "label", "toLabel"],
            [
                Assert.Throws<ArgumentNullException>(() => new ItemKeys(null!, both)).ParamName,
                Assert.Throws<ArgumentNullException>(() => new ItemKeys(derivation, null!)).ParamName,
                Assert.Throws<ArgumentException>(() => keys.Create(" ")).ParamName,
                Assert.Throws<ArgumentException>(() => keys.Rewrap(1, [], 2, " ")).ParamName,
            ]);
    }

    private static CustodyEpochs Epochs(params int[] numbers) => Epochs(numbers.Select(number => (number, number)).ToArray());

    private static CustodyEpochs Epochs(params (int Number, int Seed)[] given) =>
        CustodyEpochs.Of(given.ToDictionary(pair => pair.Number, pair => Enumerable.Range(0, MasterKey.Length).Select(index => (byte)(0xA7 ^ (index * 31) ^ (pair.Seed * 11))).ToArray()));

    private ItemKeys Keys(CustodyEpochs epochs) => new(new KeyDerivation(master, DerivationLabels.Of([Label, Next])), epochs);
}
