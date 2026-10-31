using Core.Platform.Logging;

namespace Core.Host.Migration;

/// <summary>The logging of the migration mode, which has no host, no file destination and no language service.</summary>
internal static class MigrationLogging
{
    /// <summary>Redacted console logging alone.</summary>
    internal static ServiceProvider Services()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton(TimeProvider.System);
        _ = services.AddLogging(PlatformLogging.Configure);
        _ = services.AddRedaction(PlatformLogging.EraseSensitiveData);
        return services.BuildServiceProvider();
    }
}
