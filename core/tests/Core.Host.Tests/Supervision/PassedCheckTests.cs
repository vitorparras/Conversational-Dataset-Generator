using Core.Platform.Startup;
using Xunit.Sdk;

namespace Core.Host.Tests.Supervision;

/// <summary>A check that did not pass fails the test with the check's own words, so the failure names where the start stopped.</summary>
public sealed class PassedCheckTests
{
    [Fact]
    public void FailedCheckFailsTheAssertionWithItsMessage()
    {
        var failed = CheckResult.Failed("LanguageService", "o serviço não respondeu em 30 s", "service.path") with { Observed = "porta anunciada" };

        var failure = Assert.Throws<TrueException>(() => PassedCheck.Assert(failed));

        Assert.Contains("o serviço não respondeu em 30 s", failure.Message, StringComparison.Ordinal);
        Assert.Contains("porta anunciada", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PassedCheckPassesTheAssertion() =>
        PassedCheck.Assert(CheckResult.Passed("LanguageService", "o serviço respondeu", "service.path"));
}
