using Core.Host.Supervision;
using Core.Platform.Configuration;
using Core.Platform.Configuration.Sources;
using Core.Platform.Startup;

namespace Core.Host;

/// <summary>Reads the closed configuration and runs the startup checks, then has the root assemble the host.</summary>
internal static class StartupSequence
{
    internal static async Task<HostLaunch> RunAsync(string[] args)
    {
        var loaded = ClosedConfiguration.Load(
            new ConfigurationSchema([HostConfiguration.Section, LanguageServiceConfiguration.Section]),
            new JsonFileSource(HostConfiguration.SettingsPath),
            EnvironmentSource.FromProcess(),
            new StrictArgumentsSource(args));
        if (loaded.Refused)
        {
            return HostLaunch.Refused(loaded.Problems);
        }

        var startup = CompositionRoot.StartupServices(loaded);
        try
        {
            var checks = new IStartupCheck[] { CompositionRoot.BundledFile(loaded), startup.GetRequiredService<LanguageServiceStartupCheck>() };
            var report = await new StartupRunner(checks).RunAsync(CancellationToken.None);
            if (!report.Ready)
            {
                return HostLaunch.CheckFailed(report);
            }

            var launch = HostLaunch.Ready(CompositionRoot.Assemble(loaded, report, startup));
            startup = null;
            return launch;
        }
        finally
        {
            if (startup is not null)
            {
                await startup.DisposeAsync();
            }
        }
    }
}
