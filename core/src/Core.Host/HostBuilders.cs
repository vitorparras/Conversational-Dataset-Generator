using Core.Platform.Logging;

namespace Core.Host;

/// <summary>The parts of the assembly that build no service of the host and so do not belong in the composition root: the web host that reads nothing of its own, and logging alone.</summary>
internal static class HostBuilders
{
    /// <summary>A web host with no configuration source of its own, so nothing reaches it except what the closed configuration approved.</summary>
    /// <param name="urls">The addresses the host listens on.</param>
    internal static WebApplicationBuilder Closed(string urls)
    {
        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions { ContentRootPath = AppContext.BaseDirectory });
        _ = builder.WebHost.UseKestrel().UseUrls(urls);
        _ = builder.Services.AddRoutingCore();
        _ = builder.Services.Configure<ConsoleLifetimeOptions>(options => options.SuppressStatusMessages = true);
        return builder;
    }

    /// <summary>Redacted console logging alone: the modes that migrate or rotate have no host, no file destination and no language service.</summary>
    internal static ServiceProvider Logging() =>
        new ServiceCollection().AddSingleton(TimeProvider.System).AddLogging(PlatformLogging.Configure).AddRedaction(PlatformLogging.EraseSensitiveData).BuildServiceProvider();
}
