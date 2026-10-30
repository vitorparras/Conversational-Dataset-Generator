using Core.Platform.Startup;

namespace Core.Host.Tests.Supervision;

/// <summary>The assertion that a check passed, which fails with what the check said and not only with the status it ended in.</summary>
internal static class PassedCheck
{
    /// <summary>Fails with the check's name, status, message and what it observed, so that a check that stopped says where.</summary>
    public static void Assert(CheckResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        Xunit.Assert.True(result.Status == CheckStatus.Passed, $"{result.Name} {result.Status}: {result.Message} (observado: {result.Observed ?? "nada"})");
    }
}
