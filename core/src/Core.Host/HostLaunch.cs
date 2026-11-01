using System.Diagnostics;
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
    /// Serves until stopped; any failure not fatal to the process, such as a port already taken, ends it with its own code. This is the
    /// one place that turns whatever stops the host into <see cref="ExitCode.ServingFailed"/>, so it catches every exception.
    /// </summary>
    private async Task<ExitCode> ServeAsync()
    {
        try
        {
            await app!.RunAsync().ConfigureAwait(false);
            return ExitCode.Success;
        }
        catch (Exception exception) when (ServingFailure.CanReport(exception))
        {
            return ServingFailure.Report(Console.Error, exception);
        }
    }
}
