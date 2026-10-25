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
        var loaded = Load(args);
        if (loaded.Refused)
        {
            return HostLaunch.Refused(loaded.Problems);
        }

        var analyzeTimeout = LanguageServiceConfiguration.AnalyzeTimeout(loaded.Values);
        return analyzeTimeout is null
            ? HostLaunch.Refused([LanguageServiceConfiguration.AnalyzeTimeoutRefused(loaded.Values)])
            : await LaunchAsync(loaded, analyzeTimeout.Value);
    }

    /// <summary>
    /// Opens the file destination and the startup container, runs the checks and, when every one passed, hands both to
    /// the host; a failed check closes them here.
    /// </summary>
    private static async Task<HostLaunch> LaunchAsync(ConfigurationLoadResult loaded, TimeSpan analyzeTimeout)
    {
        var fileLog = OpenFileLog(loaded);
        var startup = StartupServices(loaded, fileLog);
        try
        {
            IStartupCheck[] checks = [.. LogFileChecks(loaded, fileLog), .. Checks(loaded, startup)];
            var report = await new StartupRunner(checks).RunAsync(CancellationToken.None);
            if (!report.Ready)
            {
                return HostLaunch.CheckFailed(report);
            }

            var launch = HostLaunch.Ready(Assemble(
                Prepared(loaded, fileLog, analyzeTimeout), report, startup, RecognizerConfiguration.KindOf(loaded.Values)));
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

    /// <summary>The closed configuration of the host: the file beside it, the process environment and the strict arguments.</summary>
    private static ConfigurationLoadResult Load(string[] args) =>
        ClosedConfiguration.Load(
            new ConfigurationSchema([HostConfiguration.Section, LanguageServiceConfiguration.Section, RecognizerConfiguration.Section]),
            new JsonFileSource(HostConfiguration.SettingsPath),
            EnvironmentSource.FromProcess(),
            new StrictArgumentsSource(args));

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

    /// <summary>The file the configuration names, relative to the binary's directory; null when absent or when it cannot be opened.</summary>
    private static FileLoggerProvider? OpenFileLog(ConfigurationLoadResult loaded) =>
        loaded.Values.TryGetValue(HostConfiguration.LogFilePath, out var path)
            ? FileLoggerProvider.Open(
                Path.Combine(AppContext.BaseDirectory, path), new LineConsoleFormatter(TimeProvider.System), Console.Error)
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
    private static ServiceProvider StartupServices(ConfigurationLoadResult loaded, FileLoggerProvider? fileLog)
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton(TimeProvider.System);
        _ = services.AddLogging(logging => WithFile(logging, fileLog));
        if (fileLog is not null)
        {
            _ = services.AddSingleton(fileLog);
        }

        _ = services.AddRedaction(PlatformLogging.EraseSensitiveData);
        _ = services.AddSingleton(_ => LoopbackHttpClient.Create());
        _ = services.AddSingleton(provider => new HealthWait(provider.GetRequiredService<HttpClient>()));
        _ = services.AddSingleton(new ServiceProcessLauncher(loaded.Values[LanguageServiceConfiguration.ExecutablePath]));
        _ = services.AddSingleton(new ServiceProcessWatch());
        _ = services.AddSingleton(provider => new LanguageServiceSupervisor(
            provider.GetRequiredService<ServiceProcessLauncher>(),
            provider.GetRequiredService<HealthWait>(),
            provider.GetRequiredService<ServiceProcessWatch>(),
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

    /// <summary>The web host with everything the configuration decides: its address, logging, the file destination and the recognizer.</summary>
    private static WebApplicationBuilder Prepared(ConfigurationLoadResult loaded, ILoggerProvider? fileLog, TimeSpan analyzeTimeout)
    {
        var builder = ClosedBuilder(loaded.Values[HostConfiguration.UrlsPath]);
        Compose(builder, loaded.View, fileLog);
        AddRecognizer(builder.Services, loaded.Values, analyzeTimeout);
        return builder;
    }

    /// <summary>
    /// What the checks produced, adopted by the host: the report the routes show and the startup container, which stops
    /// the service when the host does; the status route also learns which recognizer is in force.
    /// </summary>
    private static WebApplication Assemble(
        WebApplicationBuilder builder, StartupReport report, ServiceProvider startup, RecognizerKind recognizer)
    {
        _ = builder.Services.AddSingleton(report);
        _ = builder.Services.AddSingleton(provider => provider.GetRequiredService<ServiceProvider>().GetRequiredService<ServiceProcessWatch>());
        _ = builder.Services.AddSingleton(_ => startup);
        var versions = new VersionReport(BuildInfo.Of(typeof(CompositionRoot).Assembly));
        _ = builder.Services.AddSingleton(versions);

        var app = builder.Build();
        _ = app.Services.GetRequiredService<ServiceProvider>();
        app.Logger.VersionsInForce(versions);
        StatusEndpoint.Map(app, recognizer);
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
