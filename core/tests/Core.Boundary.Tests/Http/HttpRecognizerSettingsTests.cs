using Core.Boundary.Http;

namespace Core.Boundary.Tests.Http;

/// <summary>Settings that would leave a recognition waiting forever, past what a timer accepts, or unauthenticated, are a defect at construction.</summary>
public sealed class HttpRecognizerSettingsTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(3_600_001)]
    public void TimeLimitOutsideItsRangeIsRefused(int milliseconds)
    {
        var limit = TimeSpan.FromMilliseconds(milliseconds);

        var defect = Assert.Throws<ArgumentOutOfRangeException>(() => new HttpRecognizerSettings(1, Reply.Secret, limit));

        Assert.Equal("timeout", defect.ParamName);
    }

    [Fact]
    public void LongestTimeLimitIsAccepted()
    {
        var settings = new HttpRecognizerSettings(1, Reply.Secret, HttpRecognizerSettings.LongestTimeout);

        Assert.Equal(TimeSpan.FromHours(1), settings.Timeout);
    }

    [Fact]
    public void EmptySecretIsRefused()
    {
        var empty = string.Empty;

        var defect = Assert.Throws<ArgumentException>(() => new HttpRecognizerSettings(1, empty, TimeSpan.FromSeconds(1)));

        Assert.Equal("secret", defect.ParamName);
    }

    [Fact]
    public void DescriptionDoesNotCarryTheSecret()
    {
        var settings = new HttpRecognizerSettings(1, Reply.Secret, TimeSpan.FromSeconds(1));

        Assert.DoesNotContain(Reply.Secret, settings.ToString(), StringComparison.Ordinal);
    }
}
