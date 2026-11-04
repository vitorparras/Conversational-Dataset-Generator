using Core.Platform.Keys;

namespace Core.Platform.Tests.Keys;

/// <summary>The derivation is the HKDF-SHA256 of RFC 5869: the vector of the RFC that has no salt, and the key of a label pinned against an independent computation.</summary>
public sealed class HkdfTests
{
    [Fact]
    public void RfcVectorWithNoSaltAndNoInfoGivesTheOutputTheRfcPrints()
    {
        var input = Enumerable.Repeat((byte)0x0b, 22).ToArray();
        var output = new byte[42];

        Hkdf.Derive(input, [], output);

        Assert.Equal("8da4e775a563c18f715f802a063c5a31b8a11f5c5ee1879ec3454e5f3c738d2d9d201395faa4b61a96c8", Convert.ToHexStringLower(output));
    }

    [Fact]
    public void RfcVectorWithSaltAndInfoGivesTheOutputTheRfcPrints()
    {
        var input = Enumerable.Repeat((byte)0x0b, 22).ToArray();
        var salt = Enumerable.Range(0, 13).Select(index => (byte)index).ToArray();
        var info = Enumerable.Range(0xf0, 10).Select(value => (byte)value).ToArray();
        var output = new byte[42];

        Hkdf.Derive(input, salt, info, output);

        Assert.Equal("3cb25f25faacd57a90434f64d0362f2a2d2d0a90cf1a5a4c5db02d56ecc4c5bf34007208d5b887185865", Convert.ToHexStringLower(output));
    }

    [Fact]
    public void KeyOfALabelIsTheHkdfOutputOfTheMasterKeyWithTheLabelAsInfo()
    {
        using var master = new MasterKey(SyntheticKey.Valid());
        using var derived = new DerivedKey(master.AsSpan(), "texto-bruto/7/v1");

        Assert.Equal("f370cfc04c6f7fde00e8b1e70ab2f2018ce6a1574b84d38c6f6605d086c95eb1", Convert.ToHexStringLower(derived.AsSpan()));
    }
}
