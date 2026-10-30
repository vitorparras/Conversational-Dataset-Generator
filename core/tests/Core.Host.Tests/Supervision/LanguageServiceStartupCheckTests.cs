using Core.Host.Supervision;
using Core.Platform.Startup;

namespace Core.Host.Tests.Supervision;

/// <summary>The check starts the service and reports it as a result; a service that cannot start is a failed check, not an exception.</summary>
public sealed class LanguageServiceStartupCheckTests : IDisposable
{
    private const string MissingExecutable = "zqx-missing-7731";

    private readonly SupervisionHarness harness = new();

    [Fact]
    public async Task ExecutableThatDoesNotExistFailsNamingTheKeyThatHoldsIt()
    {
        var supervisor = harness.SupervisorFor(MissingExecutable);
        var check = new LanguageServiceStartupCheck(supervisor, "5");

        var result = await check.RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.Equal(LanguageServiceConfiguration.ExecutablePath, result.ExpectedSource);
        Assert.Null(supervisor.ProcessId);
    }

    [Trait("Category", "RealService")]
    [Fact]
    public async Task TimeLimitThatIsNotAPositiveWholeNumberFailsNamingTheKeyThatHoldsIt()
    {
        var supervisor = harness.SupervisorFor(LanguageServiceInterpreter.Path);
        var check = new LanguageServiceStartupCheck(supervisor, "0");

        var result = await check.RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.Equal(LanguageServiceConfiguration.StartupTimeoutPath, result.ExpectedSource);
        Assert.Null(supervisor.ProcessId);
    }

    [Trait("Category", "RealService")]
    [Fact]
    public async Task ServiceThatAnswersPassesTheCheckAndStaysRunning()
    {
        var supervisor = harness.SupervisorFor(LanguageServiceInterpreter.Path);
        var check = new LanguageServiceStartupCheck(supervisor, "30");

        var result = await check.RunAsync(TestContext.Current.CancellationToken);

        PassedCheck.Assert(result);
        Assert.True(ProcessTree.IsRunning(supervisor.ProcessId!.Value));
    }

    public void Dispose() => harness.Dispose();
}
