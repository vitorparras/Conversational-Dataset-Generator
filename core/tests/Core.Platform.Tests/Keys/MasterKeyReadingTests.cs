using Core.Platform.Keys;

namespace Core.Platform.Tests.Keys;

/// <summary>A reading is the key with its labels or the reason there is none, and it takes the bytes it was given so that no other copy stays behind.</summary>
public sealed class MasterKeyReadingTests
{
    [Fact]
    public void BytesOfTheRightSizeMakeTheKeyAndKeepTheLabels()
    {
        var bytes = SyntheticKey.Valid();
        var expected = SyntheticKey.Valid();
        using var reading = MasterKeyReading.FromBytes(bytes, DerivationLabels.Of(["dominio-a"]));

        Assert.Null(reading.Failure);
        Assert.True(reading.Key!.AsSpan().SequenceEqual(expected));
        Assert.Equal(["dominio-a"], reading.Labels.All);
    }

    [Fact]
    public void BytesGivenAreZeroedOnceTheKeyHoldsItsCopy()
    {
        var bytes = SyntheticKey.Valid();
        using var reading = MasterKeyReading.FromBytes(bytes, DerivationLabels.None);

        Assert.All(bytes, value => Assert.Equal(0, value));
    }

    [Fact]
    public void BytesOfOtherSizeAreZeroedAndGiveNoKey()
    {
        var bytes = SyntheticKey.OfLength(16);
        using var reading = MasterKeyReading.FromBytes(bytes, DerivationLabels.None);

        Assert.Null(reading.Key);
        Assert.All(bytes, value => Assert.Equal(0, value));
    }

    [Fact]
    public void BytesOfOtherSizeSayWrongSizeAndTheSizeFound()
    {
        using var reading = MasterKeyReading.FromBytes(SyntheticKey.OfLength(16), DerivationLabels.Of(["dominio-a"]));

        Assert.Equal(new MasterKeyFailure(MasterKeyFailureKind.WrongSize, "16 bytes, esperados 32"), reading.Failure);
    }

    [Fact]
    public void BytesOfOtherSizeNameTheSourceTheyCameFromWhenItIsGiven()
    {
        using var reading = MasterKeyReading.FromBytes(SyntheticKey.OfLength(16), DerivationLabels.None, "arquivo da chave");

        Assert.Equal(new MasterKeyFailure(MasterKeyFailureKind.WrongSize, "arquivo da chave: 16 bytes, esperados 32"), reading.Failure);
    }

    [Fact]
    public void BytesOfTheRightSizeAreTakenWhateverTheSourceNamed()
    {
        using var reading = MasterKeyReading.FromBytes(SyntheticKey.Valid(), DerivationLabels.None, "arquivo da chave");

        Assert.Null(reading.Failure);
    }

    [Fact]
    public void BytesWithoutASourceAreADefect() =>
        _ = Assert.Throws<ArgumentNullException>(() => MasterKeyReading.FromBytes(SyntheticKey.Valid(), DerivationLabels.None, null!));

    [Fact]
    public void ReadingWithoutAKeyHoldsNoLabelEvenIfTheSourceHadSome()
    {
        using var reading = MasterKeyReading.FromBytes(SyntheticKey.OfLength(16), DerivationLabels.Of(["dominio-a"]));

        Assert.Empty(reading.Labels.All);
    }

    [Fact]
    public void AbsentReadingSaysWhatIsMissing()
    {
        using var reading = MasterKeyReading.Absent("o caminho não foi informado");

        Assert.Null(reading.Key);
        Assert.Equal(new MasterKeyFailure(MasterKeyFailureKind.Absent, "o caminho não foi informado"), reading.Failure);
    }

    [Fact]
    public void UnreadableReadingSaysWhatWasFound()
    {
        using var reading = MasterKeyReading.Unreadable("texto que não é base64");

        Assert.Null(reading.Key);
        Assert.Equal(new MasterKeyFailure(MasterKeyFailureKind.Unreadable, "texto que não é base64"), reading.Failure);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void FailureThatSaysNothingIsADefect(string said)
    {
        _ = Assert.Throws<ArgumentException>(() => MasterKeyReading.Absent(said));
        _ = Assert.Throws<ArgumentException>(() => MasterKeyReading.Unreadable(said));
    }

    [Fact]
    public void ReleasingTheReadingReleasesTheKey()
    {
        var reading = MasterKeyReading.FromBytes(SyntheticKey.Valid(), DerivationLabels.None);
        var key = reading.Key!;

        reading.Dispose();

        _ = Assert.Throws<ObjectDisposedException>(() => key.AsSpan().ToArray());
    }

    [Fact]
    public void ReleasingAReadingWithoutAKeyIsHarmless()
    {
        var reading = MasterKeyReading.Absent("nada");

        var released = Record.Exception(reading.Dispose);

        Assert.Null(released);
    }

    [Fact]
    public void NoBytesOrNoLabelsIsADefect()
    {
        _ = Assert.Throws<ArgumentNullException>(() => MasterKeyReading.FromBytes(null!, DerivationLabels.None));
        _ = Assert.Throws<ArgumentNullException>(() => MasterKeyReading.FromBytes(SyntheticKey.Valid(), null!));
    }
}
