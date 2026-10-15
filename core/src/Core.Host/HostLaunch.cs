using Core.Platform.Configuration;
using Core.Platform.Startup;

namespace Core.Host;

/// <summary>The assembled host, or the refusal that kept it from being assembled.</summary>
internal sealed class HostLaunch
{
    private readonly WebApplication? app;
    private readonly IReadOnlyList<ConfigurationProblem> problems;

    private HostLaunch(WebApplication? app, IReadOnlyList<ConfigurationProblem> problems)
    {
        this.app = app;
        this.problems = problems;
    }

    public static HostLaunch Ready(WebApplication app) => new(app, []);

    public static HostLaunch Refused(IReadOnlyList<ConfigurationProblem> problems) => new(app: null, problems);

    /// <summary>Serves until stopped, or reports the refusal and ends at once.</summary>
    public ExitCode Run()
    {
        if (app is null)
        {
            return ConfigurationRefusal.Report(Console.Error, problems);
        }

        app.Run();
        return ExitCode.Success;
    }
}
