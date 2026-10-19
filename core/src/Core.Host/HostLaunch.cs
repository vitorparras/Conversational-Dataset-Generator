using System.Diagnostics;
using Core.Platform.Configuration;
using Core.Platform.Startup;

namespace Core.Host;

/// <summary>The assembled host, or the refusal that kept it from being assembled.</summary>
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

    /// <summary>Serves until stopped, or reports why the host was not assembled and ends at once.</summary>
    internal ExitCode Run() => state switch
    {
        HostLaunchState.Ready => Serve(),
        HostLaunchState.Refused => ConfigurationRefusal.Report(Console.Error, problems),
        HostLaunchState.CheckFailed => StartupRefusal.Report(Console.Error, report!),
        _ => throw new UnreachableException(),
    };

    private ExitCode Serve()
    {
        app!.Run();
        return ExitCode.Success;
    }
}
