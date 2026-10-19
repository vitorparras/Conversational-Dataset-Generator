using System.Text.Json.Serialization;
using Core.Host.Endpoints;
using Core.Host.Supervision;
using Core.Platform.Configuration;
using Core.Platform.Configuration.Sources;
using Core.Platform.Logging;
using Core.Platform.Startup;

namespace Core.Host;

/// <summary>The only place where the host is assembled: configuration first, then logging and services, then routes.</summary>
internal static class CompositionRoot
{
    internal static async Task<HostLaunch> BuildAsync(string[] args)
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

        var fileLog = OpenFileLog(loaded);
        var startup = StartupServices(loaded, fileLog);
        try
        {
            IStartupCheck[] checks =
                [.. LogFileChecks(loaded, fileLog), BundledFile(loaded), startup.GetRequiredService<LanguageServiceStartupCheck>()];
            var report = await new StartupRunner(checks).RunAsync(CancellationToken.None);
            if (!report.Ready)
            {
                return HostLaunch.CheckFailed(report);
            }

            var launch = HostLaunch.Ready(Assemble(loaded, report, startup, fileLog));
            startup = null;
            fileLog = null;
            return launch;
        }
        finally
        {
            if (startup is not null)
            {
                await startup.DisposeAsync();
            }

            fileLog?.Dispose();
        }
    }

    /// <summary>
    /// Logging with redaction before anything else, then every service the routes consume. The host's container owns
    /// the file destination and, since every logger depends on it, closes it after the last of them.
    /// </summary>
    internal static void Compose(IHostApplicationBuilder builder, EffectiveConfiguration configuration, ILoggerProvider? fileLog)
    {
        PlatformLogging.Configure(builder.Logging);
        if (fileLog is not null)
        {
            _ = builder.Services.AddSingleton(_ => fileLog);
        }

        _ = builder.Services.AddSingleton(TimeProvider.System);
        _ = builder.Services.AddRedaction(PlatformLogging.EraseSensitiveData);
        _ = builder.Services.AddSingleton(configuration);
        _ = builder.Services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
    }

    /// <summary>The file the configuration names, relative to the binary's directory; null when absent or when it cannot be opened.</summary>
    private static FileLoggerProvider? OpenFileLog(ConfigurationLoadResult loaded) =>
        loaded.Values.TryGetValue(HostConfiguration.LogFilePath, out var path)
            ? FileLoggerProvider.Open(Path.Combine(AppContext.BaseDirectory, path), new LineConsoleFormatter(TimeProvider.System))
            : null;

    /// <summary>A check on the log file only when the configuration names one; without it the log goes to the console alone.</summary>
    private static IEnumerable<IStartupCheck> LogFileChecks(ConfigurationLoadResult loaded, ILoggerProvider? fileLog) =>
        loaded.Values.ContainsKey(HostConfiguration.LogFilePath)
            ? [new LogFileCheck(HostConfiguration.LogFilePath, opened: fileLog is not null)]
            : [];

    private static BundledFileCheck BundledFile(ConfigurationLoadResult loaded) =>
        new(HostConfiguration.BundledFilePath, loaded.Values[HostConfiguration.BundledFilePath], AppContext.BaseDirectory);

    /// <summary>
    /// What the checks need before the host's own container exists: redacted logging and the supervision of
    /// the language service. The host's container adopts it, so the service stops when the host does; it only borrows
    /// the file destination, which the host's container closes.
    /// </summary>
    private static ServiceProvider StartupServices(ConfigurationLoadResult loaded, ILoggerProvider? fileLog)
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton(TimeProvider.System);
        _ = services.AddLogging(logging => WithFile(logging, fileLog));
        _ = services.AddRedaction(PlatformLogging.EraseSensitiveData);
        _ = services.AddSingleton(_ => HealthWait.NewClient());
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

    /// <summary>The platform's logging plus the file, registered as an instance so this container never closes it.</summary>
    private static void WithFile(ILoggingBuilder logging, ILoggerProvider? fileLog)
    {
        PlatformLogging.Configure(logging);
        if (fileLog is not null)
        {
            _ = logging.AddProvider(fileLog);
        }
    }

    private static WebApplication Assemble(
        ConfigurationLoadResult loaded, StartupReport report, ServiceProvider startup, ILoggerProvider? fileLog)
    {
        var builder = ClosedBuilder(loaded.Values[HostConfiguration.UrlsPath]);
        Compose(builder, loaded.View, fileLog);
        _ = builder.Services.AddSingleton(report);
        _ = builder.Services.AddSingleton(_ => startup);

        var app = builder.Build();
        _ = app.Services.GetRequiredService<ServiceProvider>();
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
