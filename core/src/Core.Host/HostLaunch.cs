using Core.Platform.Configuration;
using Core.Platform.Startup;

namespace Core.Host;

/// <summary>The assembled host, or the refusal that kept it from being assembled.</summary>
internal sealed class HostLaunch
{
    private readonly HostLaunchState state;
    private readonly WebApplication? app;
    private readonly IReadOnlyList<ConfigurationProblem> problems;

    private HostLaunch(HostLaunchState state, WebApplication? app, IReadOnlyList<ConfigurationProblem> problems)
    {
        this.state = state;
        this.app = app;
        this.problems = problems;
    }

    public static HostLaunch Ready(WebApplication app) => new(HostLaunchState.Ready, app, []);

    public static HostLaunch Refused(IReadOnlyList<ConfigurationProblem> problems) =>
        new(HostLaunchState.Refused, app: null, problems);

    /// <summary>Serves until stopped, or reports the refusal and ends at once.</summary>
    public ExitCode Run() =>
        state == HostLaunchState.Refused ? ConfigurationRefusal.Report(Console.Error, problems) : Serve();

    private ExitCode Serve()
    {
        app!.Run();
        return ExitCode.Success;
    }
}
