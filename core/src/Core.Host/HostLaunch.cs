using System.Diagnostics;
using System.Net.Sockets;
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
    internal ExitCode Run() => state switch
    {
        HostLaunchState.Ready => Serve(),
        HostLaunchState.Refused => ConfigurationRefusal.Report(Console.Error, problems),
        HostLaunchState.CheckFailed => StartupRefusal.Report(Console.Error, report!),
        HostLaunchState.Migrated => MigrationExit,
        _ => throw new UnreachableException(),
    };

    /// <summary>Serves until stopped; a port already taken or an address Kestrel cannot bind ends it with its own code instead of an unhandled exception.</summary>
    private ExitCode Serve()
    {
        try
        {
            app!.Run();
            return ExitCode.Success;
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or SocketException or UnauthorizedAccessException)
        {
            return ServingFailure.Report(Console.Error, exception);
        }
    }
}
