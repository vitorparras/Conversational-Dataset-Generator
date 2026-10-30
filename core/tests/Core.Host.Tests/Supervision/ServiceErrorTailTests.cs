using Core.Host.Supervision;

namespace Core.Host.Tests.Supervision;

/// <summary>
/// The standard error of the service reaches the console of the operator, as it always did, unless a collector was asked for; a start that ends before the service answers
/// then says how, with the exit code and the end of what the service wrote. The text is the service's own and never goes to the log. The base interpreter of the environment
/// stands for a service that ends at once: it does not have the module of the service, so it writes that to its standard error and leaves with a failure code.
/// </summary>
public sealed class ServiceErrorTailTests : IDisposable
{
    private const string MissingModule = "No module named language_service";

    private readonly SupervisionHarness harness = new();

    [Fact]
    public void LauncherWithoutACollectorLeavesTheStandardErrorToTheCore()
    {
        var info = new ServiceProcessLauncher(LanguageServiceInterpreter.Standalone).StartInfo();

        Assert.False(info.RedirectStandardError);
    }

    [Fact]
    public void LauncherWithACollectorRedirectsTheStandardError()
    {
        var info = new ServiceProcessLauncher(LanguageServiceInterpreter.Standalone, new ErrorCollector()).StartInfo();

        Assert.True(info.RedirectStandardError);
    }

    [Fact]
    public void ServiceStartedWithoutACollectorHasNoErrorTail()
    {
        using var service = new ServiceProcessLauncher(LanguageServiceInterpreter.Standalone).Start(SupervisionHarness.NewSecret());

        Assert.Null(service!.Errors);
    }

    [Fact]
    public async Task ServiceStartedWithACollectorHasTheEndOfWhatItWroteToItsStandardError()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var service = new ServiceProcessLauncher(LanguageServiceInterpreter.Standalone, new ErrorCollector()).Start(SupervisionHarness.NewSecret());

        await service!.WaitForExitAsync().WaitAsync(SupervisionHarness.Timeout, cancellationToken);
        var tail = await service.Errors!.TextAsync(SupervisionHarness.Timeout);

        Assert.Contains(MissingModule, tail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TailKeepsOnlyTheLastCharactersUpToTheLimit()
    {
        var written = string.Concat(Enumerable.Range(0, 3000).Select(number => $"{number:D4};"));
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(written));
        using var streamReader = new StreamReader(stream);

        var tail = await new ErrorTail(streamReader, new ErrorCollector().Limit).TextAsync(SupervisionHarness.Timeout);

        Assert.Equal(new ErrorCollector().Limit, tail.Length);
        Assert.Equal(written[^new ErrorCollector().Limit..], tail);
    }

    [Fact]
    public async Task TailShorterThanTheLimitIsKeptWhole()
    {
        using var stream = new MemoryStream("Traceback: boom"u8.ToArray());
        using var reader = new StreamReader(stream);

        var tail = await new ErrorTail(reader, new ErrorCollector().Limit).TextAsync(SupervisionHarness.Timeout);

        Assert.Equal("Traceback: boom", tail);
    }

    [Fact]
    public async Task SupervisorWhoseServiceEndsBeforeAnsweringKeepsTheExitCodeAndTheEndOfItsStandardError()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var supervisor = harness.SupervisorFor(LanguageServiceInterpreter.Standalone);

        var outcome = await supervisor.StartAsync(SupervisionHarness.Timeout, cancellationToken);

        Assert.Equal(StartOutcome.ProcessExited, outcome);
        Assert.NotEqual(0, supervisor.ExitCode);
        Assert.Contains(MissingModule, supervisor.ExitErrorTail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SupervisorNeverWritesTheStandardErrorOfTheServiceToTheLog()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var supervisor = harness.SupervisorFor(LanguageServiceInterpreter.Standalone);

        _ = await supervisor.StartAsync(SupervisionHarness.Timeout, cancellationToken);

        Assert.NotEmpty(harness.Log.Lines);
        Assert.DoesNotContain(harness.Log.Lines, line => line.Contains("No module named", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SupervisorWithoutACollectorKeepsTheExitCodeAndNoErrorTail()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var supervisor = new LanguageServiceSupervisor(
            new ServiceProcessLauncher(LanguageServiceInterpreter.Standalone), harness.Health, new ServiceProcessWatch(), harness.Log);

        var outcome = await supervisor.StartAsync(SupervisionHarness.Timeout, cancellationToken);

        Assert.Equal(StartOutcome.ProcessExited, outcome);
        Assert.NotEqual(0, supervisor.ExitCode);
        Assert.Null(supervisor.ExitErrorTail);
    }

    [Fact]
    public async Task StartThatEndsBeforeTheServiceAnswersFailsTheTestWithTheExitCodeAndTheEndOfItsStandardError()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var supervisor = harness.SupervisorFor(LanguageServiceInterpreter.Standalone);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => SupervisionHarness.StartedAsync(supervisor, cancellationToken));

        Assert.Contains("ProcessExited", failure.Message, StringComparison.Ordinal);
        Assert.Contains($"exit code {supervisor.ExitCode}", failure.Message, StringComparison.Ordinal);
        Assert.Contains(MissingModule, failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MessageOfAProcessThatDidNotEndSaysNothingOfAnExit() => Assert.Equal(string.Empty, SupervisionHarness.Ended(null, null));

    [Fact]
    public void MessageOfAProcessThatEndedWithNothingOnItsStandardErrorSaysSo() => Assert.Equal(" (exit code 3; end of its standard error: none)", SupervisionHarness.Ended(3, "  "));

    public void Dispose() => harness.Dispose();
}
