using System.Diagnostics;
using System.Runtime.ExceptionServices;
using Core.Host.Migration;
using Core.Persistence.Migrations;
using Core.Platform.Configuration;
using Core.Platform.Startup;

namespace Core.Host;

/// <summary>The assembled host, the refusal that kept it from being assembled, or how a migration ended.</summary>
internal sealed class HostLaunch
{
    private readonly HostLaunchState state;
    private readonly WebApplication? app;
    private readonly IReadOnlyList<ConfigurationProblem> problems;
    private readonly StartupReport? report;

    private HostLaunch(
        HostLaunchState state, WebApplication? app, IReadOnlyList<ConfigurationProblem> problems, StartupReport? report)
    {
        this.state = state;
        this.app = app;
        this.problems = problems;
        this.report = report;
    }

    internal static HostLaunch Ready(WebApplication app) => new(HostLaunchState.Ready, app, [], report: null);

    internal static HostLaunch Refused(IReadOnlyList<ConfigurationProblem> problems) =>
        new(HostLaunchState.Refused, app: null, problems, report: null);

    internal static HostLaunch CheckFailed(StartupReport report) =>
        new(HostLaunchState.CheckFailed, app: null, [], report);

    private ExitCode MigrationExit { get; init; }

    internal static HostLaunch Migrated(MigrationOutcome outcome) =>
        new(HostLaunchState.Migrated, app: null, [], report: null) { MigrationExit = MigrationMode.ExitOf(outcome) };

    /// <summary>Serves until stopped, or reports why the host was not assembled and ends at once.</summary>
    internal async Task<ExitCode> RunAsync() => state switch
    {
        HostLaunchState.Ready => await ServeAsync().ConfigureAwait(false),
        HostLaunchState.Refused => ConfigurationRefusal.Report(Console.Error, problems),
        HostLaunchState.CheckFailed => StartupRefusal.Report(Console.Error, report!),
        HostLaunchState.Migrated => MigrationExit,
        _ => throw new UnreachableException(),
    };

    /// <summary>
    /// Serves until stopped; any failure not fatal to the process, such as a port already taken, ends it with its own code. The run is
    /// awaited for its end and not for its result, so the process reports what stopped it without a catch of every exception.
    /// </summary>
    private async Task<ExitCode> ServeAsync()
    {
        var run = app!.RunAsync();
        _ = await Task.WhenAny(run).ConfigureAwait(false);
        return run.IsCompletedSuccessfully ? ExitCode.Success : Ended(run);
    }

    private static ExitCode Ended(Task run)
    {
        var failure = run.Exception?.InnerExceptions[0] ?? new OperationCanceledException();
        if (!ServingFailure.CanReport(failure))
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }

        return ServingFailure.Report(Console.Error, failure);
    }
}
