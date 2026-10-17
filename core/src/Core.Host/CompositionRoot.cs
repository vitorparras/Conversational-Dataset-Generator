using System.Text.Json.Serialization;
using Core.Host.Endpoints;
using Core.Platform.Configuration;
using Core.Platform.Configuration.Sources;
using Core.Platform.Logging;
using Core.Platform.Startup;

namespace Core.Host;

/// <summary>The only place where the host is assembled: configuration first, then logging and services, then routes.</summary>
internal static class CompositionRoot
{
    public static async Task<HostLaunch> BuildAsync(string[] args)
    {
        var loaded = ClosedConfiguration.Load(
            new ConfigurationSchema([HostConfiguration.Section]),
            new JsonFileSource(HostConfiguration.SettingsPath),
            EnvironmentSource.FromProcess(),
            new StrictArgumentsSource(args));
        if (loaded.Refused)
        {
            return HostLaunch.Refused(loaded.Problems);
        }

        var report = await new StartupRunner(Checks(loaded)).RunAsync(CancellationToken.None);
        return report.Ready ? HostLaunch.Ready(Assemble(loaded, report)) : HostLaunch.CheckFailed(report);
    }

    /// <summary>Logging with redaction before anything else, then every service the routes consume.</summary>
    public static void Compose(WebApplicationBuilder builder, EffectiveConfiguration configuration)
    {
        PlatformLogging.Configure(builder.Logging);
        _ = builder.Services.AddRedaction(PlatformLogging.EraseSensitiveData);
        _ = builder.Services.AddSingleton(configuration);
        _ = builder.Services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
    }

    private static IReadOnlyList<IStartupCheck> Checks(ConfigurationLoadResult loaded) =>
    [
        new BundledFileCheck(
            HostConfiguration.BundledFilePath, loaded.Values[HostConfiguration.BundledFilePath], AppContext.BaseDirectory),
    ];

    private static WebApplication Assemble(ConfigurationLoadResult loaded, StartupReport report)
    {
        var builder = ClosedBuilder(loaded.Values[HostConfiguration.UrlsPath]);
        Compose(builder, loaded.View);
        _ = builder.Services.AddSingleton(report);

        var app = builder.Build();
        StatusEndpoint.Map(app);
        ConfigurationEndpoint.Map(app);
        return app;
    }

    /// <summary>A web host with no configuration source of its own, so nothing reaches it except what the closed configuration approved.</summary>
    internal static WebApplicationBuilder ClosedBuilder(string urls)
    {
        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions { ContentRootPath = AppContext.BaseDirectory });
        _ = builder.WebHost.UseKestrel().UseUrls(urls);
        _ = builder.Services.AddRoutingCore();
        _ = builder.Services.Configure<ConsoleLifetimeOptions>(options => options.SuppressStatusMessages = true);
        return builder;
    }
}
