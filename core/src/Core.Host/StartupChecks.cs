using Core.Host.Supervision;
using Core.Platform.Configuration;
using Core.Platform.Startup;

namespace Core.Host;

/// <summary>The checks the host runs once the log file's own is done: the bundled file, the server's, then the language service.</summary>
internal static class StartupChecks
{
    /// <summary>The language service comes last, and only when the recognizer is not the fake, which asks none and so does not start it.</summary>
    internal static IStartupCheck[] Of(ConfigurationLoadResult loaded, IServiceProvider startup) =>
        RecognizerConfiguration.UsesFake(loaded.Values)
            ? [BundledFile(loaded), .. startup.GetServices<IStartupCheck>()]
            : [BundledFile(loaded), .. startup.GetServices<IStartupCheck>(), startup.GetRequiredService<LanguageServiceStartupCheck>()];

    private static BundledFileCheck BundledFile(ConfigurationLoadResult loaded) =>
        new(HostConfiguration.BundledFilePath, loaded.Values[HostConfiguration.BundledFilePath], AppContext.BaseDirectory);
}
