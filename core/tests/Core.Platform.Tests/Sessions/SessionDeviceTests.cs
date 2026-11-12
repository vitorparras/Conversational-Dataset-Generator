using Core.Platform.Sessions;

namespace Core.Platform.Tests.Sessions;

/// <summary>The short description of the device (issue 758): a browser and a system out of fixed lists, never a version, an address or the raw text.</summary>
public sealed class SessionDeviceTests
{
    private const string Windows = "Windows NT 10.0; Win64; x64";

    [Theory]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:128.0) Gecko/20100101 Firefox/128.0", "Firefox no Windows")]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36", "Chrome no Windows")]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36 Edg/126.0.0.0", "Edge no Windows")]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36 OPR/112.0.0.0", "Opera no Windows")]
    [InlineData("Opera/9.80 (Windows NT 6.1) Presto/2.12.388 Version/12.16", "Opera no Windows")]
    [InlineData("Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.5 Safari/605.1.15", "Safari no macOS")]
    [InlineData("Mozilla/5.0 (X11; Linux x86_64; rv:128.0) Gecko/20100101 Firefox/128.0", "Firefox no Linux")]
    [InlineData("Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Mobile Safari/537.36", "Chrome no Android")]
    [InlineData("Mozilla/5.0 (iPhone; CPU iPhone OS 17_5 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.5 Mobile/15E148 Safari/604.1", "Safari no iOS")]
    [InlineData("Mozilla/5.0 (iPad; CPU OS 17_5 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) CriOS/126.0.0.0 Mobile/15E148 Safari/604.1", "Chrome no iOS")]
    [InlineData("Mozilla/5.0 (X11; CrOS x86_64 14541.0.0) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36", "Chrome no ChromeOS")]
    public void EachBrowserAndEachSystemIsNamedWithNoVersion(string agent, string expected) =>
        Assert.Equal(expected, SessionDevice.Describe(agent));

    [Theory]
    [InlineData("Edg/1 Chrome/1 Safari/1", "Edge")]
    [InlineData("OPR/1 Chrome/1 Safari/1", "Opera")]
    [InlineData("Chrome/1 Safari/1", "Chrome")]
    [InlineData("Version/1 Safari/1", "Safari")]
    public void TheBrowserThatAlsoSaysAnotherIsTheOneThatIsNamed(string agent, string expected) =>
        Assert.Equal(expected, SessionDevice.Describe(agent));

    [Theory]
    [InlineData("(Android 14; Linux)", "Navegador não identificado no Android")]
    [InlineData("(iPhone; like Mac OS X)", "Navegador não identificado no iOS")]
    [InlineData("(Macintosh)", "Navegador não identificado no macOS")]
    public void TheSystemThatAlsoSaysAnotherIsTheOneThatIsNamed(string agent, string expected) =>
        Assert.Equal(expected, SessionDevice.Describe(agent));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("curl/8.5.0")]
    public void AnAgentThatIsAbsentEmptyOrUnknownIsNotIdentified(string? agent) =>
        Assert.Equal(SessionDevice.Unknown, SessionDevice.Describe(agent));

    [Fact]
    public void OnlyTheRecognizedPartIsGiven()
    {
        Assert.Equal("Firefox", SessionDevice.Describe("Firefox/128.0"));
        Assert.Equal("Navegador não identificado no Linux", SessionDevice.Describe("(X11; Linux x86_64)"));
    }

    [Fact]
    public void AnAgentAboveTheLimitIsNotReadEvenIfItNamesABrowser()
    {
        var tooLong = $"Firefox ({Windows}) {new string('x', SessionDevice.MaxAgentLength)}";
        var atTheLimit = "Firefox " + new string('x', SessionDevice.MaxAgentLength - "Firefox ".Length);

        Assert.Equal(SessionDevice.Unknown, SessionDevice.Describe(tooLong));
        Assert.Equal("Firefox", SessionDevice.Describe(atTheLimit));
    }

    [Fact]
    public void NoVersionNorTheRawTextReachesTheDescription()
    {
        var described = SessionDevice.Describe("Mozilla/5.0 (Windows NT 10.0; rv:128.0) Gecko/20100101 Firefox/128.0 zqxmarca");

        Assert.DoesNotContain("128", described, StringComparison.Ordinal);
        Assert.DoesNotContain("zqxmarca", described, StringComparison.Ordinal);
        Assert.DoesNotContain("10.0", described, StringComparison.Ordinal);
    }
}
