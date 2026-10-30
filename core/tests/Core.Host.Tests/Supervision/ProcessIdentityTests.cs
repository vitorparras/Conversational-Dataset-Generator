namespace Core.Host.Tests.Supervision;

/// <summary>
/// The helpers that check or end a process by its number act on the pair of number and start time read earlier: the system hands a number to another
/// process once the first ends, so a stranger holding the number must neither be killed nor be taken for the process that was read. The reuse is simulated
/// by the pair of a running process with a start time that is not its own, which is what the number looks like once another process holds it.
/// </summary>
public sealed class ProcessIdentityTests
{
    private static readonly TimeSpan Reuse = TimeSpan.FromHours(1);

    [Fact]
    public void RunningProcessIsRunningUnderTheIdentityReadFromIt()
    {
        using var service = StandInService.Start(LanguageServiceInterpreter.Standalone);
        var identity = ProcessIdentity.Required(service.ProcessId);

        Assert.True(ProcessTree.IsRunning(identity));
    }

    [Fact]
    public void ReadingsOfTheStartTimeThatDifferByMillisecondsStillNameTheSameProcess()
    {
        using var service = StandInService.Start(LanguageServiceInterpreter.Standalone);
        var identity = ProcessIdentity.Required(service.ProcessId);
        var jittered = identity with { Started = identity.Started.AddMilliseconds(50) };

        Assert.True(ProcessTree.IsRunning(jittered));
    }

    [Fact]
    public void NumberHeldByAProcessThatStartedAtAnotherTimeIsNotRunningUnderTheIdentityRead()
    {
        using var service = StandInService.Start(LanguageServiceInterpreter.Standalone);
        var stranger = ForeignTo(ProcessIdentity.Required(service.ProcessId));

        Assert.False(ProcessTree.IsRunning(stranger));
        Assert.True(ProcessTree.IsRunning(service.ProcessId));
    }

    [Fact]
    public async Task ProcessThatEndedIsNotRunningUnderTheIdentityRead()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var service = StandInService.Start(LanguageServiceInterpreter.Standalone);
        var identity = ProcessIdentity.Required(service.ProcessId);

        service.Dispose();

        Assert.True(await Poll.UntilAsync(() => Task.FromResult(!ProcessTree.IsRunning(identity)), Poll.LossLimit, cancellationToken), "the end of the process was not seen");
    }

    [Fact]
    public void NumberOfNoProcessHasNoIdentity() => Assert.Null(ProcessIdentity.Of(int.MaxValue));

    [Fact]
    public async Task KillOfTheIdentityReadEndsTheProcess()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var service = StandInService.Start(LanguageServiceInterpreter.Standalone);
        var identity = ProcessIdentity.Required(service.ProcessId);

        var killed = StandInService.Kill(identity);

        Assert.True(killed);
        Assert.True(await Poll.UntilAsync(() => Task.FromResult(!ProcessTree.IsRunning(identity)), Poll.LossLimit, cancellationToken), "the process outlived the kill");
    }

    [Fact]
    public void KillLeavesAloneANumberHeldByAProcessThatStartedAtAnotherTime()
    {
        using var service = StandInService.Start(LanguageServiceInterpreter.Standalone);
        var identity = ProcessIdentity.Required(service.ProcessId);

        var killed = StandInService.Kill(ForeignTo(identity));

        Assert.False(killed);
        Assert.True(ProcessTree.IsRunning(identity));
    }

    [Fact]
    public void KillOfAProcessThatAlreadyEndedKillsNothing()
    {
        var service = StandInService.Start(LanguageServiceInterpreter.Standalone);
        var identity = ProcessIdentity.Required(service.ProcessId);
        service.Dispose();

        Assert.False(StandInService.Kill(identity));
    }

    [Fact]
    public async Task SurvivorsKillTheProcessesStillRunningOnDispose()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var service = StandInService.Start(LanguageServiceInterpreter.Standalone);
        var identity = ProcessIdentity.Required(service.ProcessId);

        new Survivors([identity]).Dispose();

        Assert.True(await Poll.UntilAsync(() => Task.FromResult(!ProcessTree.IsRunning(identity)), Poll.LossLimit, cancellationToken), "the survivor was left running");
    }

    [Fact]
    public async Task SurvivorsReadByNumberKillTheProcessStillRunningOnDispose()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var service = StandInService.Start(LanguageServiceInterpreter.Standalone);
        var identity = ProcessIdentity.Required(service.ProcessId);

        new Survivors([service.ProcessId]).Dispose();

        Assert.True(await Poll.UntilAsync(() => Task.FromResult(!ProcessTree.IsRunning(identity)), Poll.LossLimit, cancellationToken), "the survivor was left running");
    }

    [Fact]
    public void SurvivorsLeaveAloneANumberHeldByAProcessThatStartedAtAnotherTime()
    {
        using var service = StandInService.Start(LanguageServiceInterpreter.Standalone);
        var identity = ProcessIdentity.Required(service.ProcessId);

        new Survivors([ForeignTo(identity)]).Dispose();

        Assert.True(ProcessTree.IsRunning(identity));
    }

    [Fact]
    public void SurvivorsOfAProcessThatAlreadyEndedDisposeWithoutFailing()
    {
        var service = StandInService.Start(LanguageServiceInterpreter.Standalone);
        var identity = ProcessIdentity.Required(service.ProcessId);
        service.Dispose();

        var exception = Record.Exception(() => new Survivors([identity]).Dispose());

        Assert.Null(exception);
    }

    /// <summary>The pair a stranger has once it holds the number of the process read: the same number, a start time that is not that process's.</summary>
    private static ProcessIdentity ForeignTo(ProcessIdentity identity) => identity with { Started = identity.Started + Reuse };
}
