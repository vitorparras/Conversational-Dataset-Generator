using Core.Platform.Configuration;

namespace Core.Tests.Unit.Platform.Configuration;

/// <summary>The format a timeout in seconds needs: digits only, and at least one.</summary>
public sealed class PositiveIntegerFormatTests
{
    private static readonly ConfigurationKey Key =
        ConfigurationKey.WithDefault("Seconds", "30", ValueFormat.PositiveInteger);

    [Theory]
    [InlineData("1")]
    [InlineData("30")]
    [InlineData("3600")]
    public void AcceptsAWholeNumberOfAtLeastOne(string value) => Assert.Null(Key.Check(value));

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("+5")]
    [InlineData("1.5")]
    [InlineData("1e3")]
    [InlineData(" 5")]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("99999999999")]
    public void RefusesAnythingElseAndSaysWhatItAccepts(string value) =>
        Assert.Equal("um número inteiro maior que zero", Key.Check(value));
}
