using System.Text.Json;
using Core.Platform.Keys;

namespace Core.Platform.Tests.Keys;

/// <summary>The key of a label is always the same for the same master key and label, differs between labels, is derived only for a loaded label, and shows nothing of its value.</summary>
public sealed class KeyDerivationTests : IDisposable
{
    private const string Label = "texto-bruto/7/v1";
    private const string Other = "texto-bruto/8/v1";

    private readonly MasterKey master = new(SyntheticKey.Valid());

    private KeyDerivation Derivation => new(master, DerivationLabels.Of([Label, Other]));

    public void Dispose() => master.Dispose();

    [Fact]
    public void LoadedLabelGivesAKeyOf32Bytes()
    {
        using var key = Derivation.Derive(Label);

        Assert.Equal(32, key.AsSpan().Length);
    }

    [Fact]
    public void SameLabelGivesTheSameKeyEveryTime()
    {
        using var first = Derivation.Derive(Label);
        using var second = Derivation.Derive(Label);

        Assert.True(first.AsSpan().SequenceEqual(second.AsSpan()));
    }

    [Fact]
    public void DifferentLabelsGiveDifferentKeys()
    {
        using var first = Derivation.Derive(Label);
        using var second = Derivation.Derive(Other);

        Assert.False(first.AsSpan().SequenceEqual(second.AsSpan()));
    }

    [Fact]
    public void KeyIsNotTheMasterKeyNorTheLabelsBytes()
    {
        using var key = Derivation.Derive(Label);

        Assert.False(key.AsSpan().SequenceEqual(master.AsSpan()));
    }

    [Fact]
    public void AnotherMasterKeyGivesAnotherKeyForTheSameLabel()
    {
        using var otherMaster = new MasterKey(SyntheticKey.OfLength(32).Select(value => (byte)(value ^ 0xFF)).ToArray());
        using var mine = Derivation.Derive(Label);
        using var theirs = new KeyDerivation(otherMaster, DerivationLabels.Of([Label])).Derive(Label);

        Assert.False(mine.AsSpan().SequenceEqual(theirs.AsSpan()));
    }

    [Fact]
    public void LabelNotLoadedIsRefusedNamingIt()
    {
        var refused = Assert.Throws<InvalidOperationException>(() => Derivation.Derive("mapa-reversao/9/v1"));

        Assert.Contains("mapa-reversao/9/v1", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NoLabelLoadedRefusesEvenTheLabelOfAnotherSet()
    {
        var derivation = new KeyDerivation(master, DerivationLabels.None);

        _ = Assert.Throws<InvalidOperationException>(() => derivation.Derive(Label));
    }

    [Fact]
    public void RefusalNamesTheLabelAndNothingOfTheMasterKey()
    {
        var refused = Assert.Throws<InvalidOperationException>(() => Derivation.Derive("mapa-reversao/9/v1"));

        Assert.All(SyntheticKey.Spellings(SyntheticKey.Valid()), spelling => Assert.DoesNotContain(spelling, refused.Message, StringComparison.Ordinal));
    }

    [Fact]
    public void ReleasedKeyHasItsBytesZeroedAndGivesNoneToRead()
    {
        var key = Derivation.Derive(Label);

        key.Dispose();

        Assert.True(key.Held.IndexOfAnyExcept((byte)0) < 0);
        _ = Assert.Throws<ObjectDisposedException>(() => key.AsSpan().Length);
    }

    [Fact]
    public void TextAndSerializationOfTheKeyNameTheTypeAndNothingOfTheValue()
    {
        using var key = Derivation.Derive(Label);
        var json = JsonSerializer.Serialize(key);

        Assert.Equal("DerivedKey", key.ToString());
        Assert.Equal("{}", json);
    }

    [Fact]
    public void NoMasterKeyIsADefect() =>
        _ = Assert.Throws<ArgumentNullException>(() => new KeyDerivation(null!, DerivationLabels.None));

    [Fact]
    public void NoLabelsIsADefect() =>
        _ = Assert.Throws<ArgumentNullException>(() => new KeyDerivation(master, null!));

    [Fact]
    public void NoLabelToDeriveIsADefect() =>
        _ = Assert.Throws<ArgumentNullException>(() => Derivation.Derive(null!));
}
