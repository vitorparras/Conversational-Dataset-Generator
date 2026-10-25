using System.Text.Json.Serialization;
using Core.Boundary.Contracts;
using Core.Boundary.Fake;
using Core.Boundary.Http;
using Core.Host.Endpoints;
using Core.Host.Supervision;
using Core.Platform.Configuration;
using Core.Platform.Configuration.Sources;
using Core.Platform.Logging;
using Core.Platform.Startup;
using Core.Platform.Versions;

namespace Core.Host;

/// <summary>The only place where the host is assembled: configuration first, then logging and services, then routes.</summary>
internal static class CompositionRoot
{
    internal static async Task<HostLaunch> BuildAsync(string[] args)
    {
        var loaded = ClosedConfiguration.Load(
            new ConfigurationSchema([HostConfiguration.Section, LanguageServiceConfiguration.Section, RecognizerConfiguration.Section]),
            new JsonFileSource(HostConfiguration.SettingsPath),
            EnvironmentSource.FromProcess(),
            new StrictArgumentsSource(args));
        if (loaded.Refused)
        {
            return HostLaunch.Refused(loaded.Problems);
        }

        var analyzeTimeout = LanguageServiceConfiguration.AnalyzeTimeout(loaded.Values);
        if (analyzeTimeout is null)
        {
            return HostLaunch.Refused([LanguageServiceConfiguration.AnalyzeTimeoutRefused(loaded.Values)]);
        }

        var startup = StartupServices(loaded);
        try
        {
            var report = await new StartupRunner(Checks(loaded, startup)).RunAsync(CancellationToken.None);
            if (!report.Ready)
            {
                return HostLaunch.CheckFailed(report);
            }

            var launch = HostLaunch.Ready(Assemble(loaded, report, startup, analyzeTimeout.Value));
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

    /// <summary>Logging with redaction before anything else, then every service the routes consume.</summary>
    internal static void Compose(IHostApplicationBuilder builder, EffectiveConfiguration configuration)
    {
        PlatformLogging.Configure(builder.Logging);
        _ = builder.Services.AddSingleton(TimeProvider.System);
        _ = builder.Services.AddRedaction(PlatformLogging.EraseSensitiveData);
        _ = builder.Services.AddSingleton(configuration);
        _ = builder.Services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
    }

    /// <summary>The recognizer chosen by configuration: the fake by name, otherwise the one of the service the supervisor started.</summary>
    internal static void AddRecognizer(IServiceCollection services, IReadOnlyDictionary<string, string> values, TimeSpan analyzeTimeout) =>
        _ = RecognizerConfiguration.UsesFake(values)
            ? services.AddSingleton<IRecognizer, FakeRecognizer>()
            : services.AddSingleton<IRecognizer>(provider => Recognizer(provider, analyzeTimeout));

    /// <summary>The bundled file, then the language service, which the fake recognizer does not ask and so is not started for it.</summary>
    private static IStartupCheck[] Checks(ConfigurationLoadResult loaded, IServiceProvider startup) =>
        RecognizerConfiguration.UsesFake(loaded.Values)
            ? [BundledFile(loaded)]
            : [BundledFile(loaded), startup.GetRequiredService<LanguageServiceStartupCheck>()];

    private static BundledFileCheck BundledFile(ConfigurationLoadResult loaded) =>
        new(HostConfiguration.BundledFilePath, loaded.Values[HostConfiguration.BundledFilePath], AppContext.BaseDirectory);

    /// <summary>
    /// What the checks need before the host's own container exists: redacted logging and the supervision of
    /// the language service. The host's container adopts it, so the service stops when the host does.
    /// </summary>
    private static ServiceProvider StartupServices(ConfigurationLoadResult loaded)
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton(TimeProvider.System);
        _ = services.AddLogging(PlatformLogging.Configure);
        _ = services.AddRedaction(PlatformLogging.EraseSensitiveData);
        _ = services.AddSingleton(_ => LoopbackHttpClient.Create());
        _ = services.AddSingleton(provider => new HealthWait(provider.GetRequiredService<HttpClient>()));
        _ = services.AddSingleton(new ServiceProcessLauncher(loaded.Values[LanguageServiceConfiguration.ExecutablePath]));
        _ = services.AddSingleton(provider => new LanguageServiceSupervisor(
            provider.GetRequiredService<ServiceProcessLauncher>(),
            provider.GetRequiredService<HealthWait>(),
            provider.GetRequiredService<ILogger<LanguageServiceSupervisor>>()));
        _ = services.AddSingleton(provider => new LanguageServiceStartupCheck(
            provider.GetRequiredService<LanguageServiceSupervisor>(), loaded.Values[LanguageServiceConfiguration.StartupTimeoutPath]));
        return services.BuildServiceProvider();
    }

    private static WebApplication Assemble(ConfigurationLoadResult loaded, StartupReport report, ServiceProvider startup, TimeSpan analyzeTimeout)
    {
        var builder = ClosedBuilder(loaded.Values[HostConfiguration.UrlsPath]);
        Compose(builder, loaded.View);
        _ = builder.Services.AddSingleton(report);
        _ = builder.Services.AddSingleton(_ => startup);
        AddRecognizer(builder.Services, loaded.Values, analyzeTimeout);
        var versions = new VersionReport(BuildInfo.Of(typeof(CompositionRoot).Assembly));
        _ = builder.Services.AddSingleton(versions);

        var app = builder.Build();
        _ = app.Services.GetRequiredService<ServiceProvider>();
        app.Logger.VersionsInForce(versions);
        StatusEndpoint.Map(app);
        ConfigurationEndpoint.Map(app);
        VersionsEndpoint.Map(app);
        return app;
    }

    /// <summary>The recognizer of the service the supervisor started, with its secret and the client that never goes through a proxy.</summary>
    private static HttpRecognizer Recognizer(IServiceProvider host, TimeSpan analyzeTimeout)
    {
        var startup = host.GetRequiredService<ServiceProvider>();
        var supervisor = startup.GetRequiredService<LanguageServiceSupervisor>();
        var port = supervisor.Port ?? throw new InvalidOperationException("The language service has no port before it has answered.");
        return new HttpRecognizer(
            startup.GetRequiredService<HttpClient>(),
            new HttpRecognizerSettings(port, supervisor.Secret, analyzeTimeout),
            host.GetRequiredService<ILogger<HttpRecognizer>>());
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
