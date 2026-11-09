using Core.Boundary.Contracts;
using Core.Boundary.Fake;
using Core.Boundary.Http;
using Core.Host.Analysis;
using Core.Host.Endpoints;
using Core.Host.Migration;
using Core.Host.Operators;
using Core.Host.Supervision;
using Core.Persistence.Connection;
using Core.Persistence.Content.Reading;
using Core.Persistence.Content.Rotation;
using Core.Persistence.Content.Sealing;
using Core.Persistence.Custody.Chain;
using Core.Persistence.Domains;
using Core.Persistence.Migrations;
using Core.Persistence.People;
using Core.Persistence.Recovery;
using Core.Persistence.ServerGuarantees;
using Core.Persistence.ServerGuarantees.Restore;
using Core.Platform.Configuration;
using Core.Platform.Keys;
using Core.Platform.Logging;
using Core.Platform.Sessions;
using Core.Platform.Startup;
using Core.Platform.Versions;
using Npgsql;

namespace Core.Host;

/// <summary>The only place where the host is assembled: the target check first, then configuration, logging and services, then routes.</summary>
internal static class CompositionRoot
{
    /// <summary>The migration mode: the steps run once with the owner credential the configuration holds, the outcome is logged and nothing is served.</summary>
    internal static async Task<HostLaunch> MigrateAsync(DatabaseSettings database)
    {
        await using var dataSource = NpgsqlDataSource.Create(database.ConnectionString!);
        await using var logging = LoggingServices();
        var result = await new MigrationRunner(dataSource, EmbeddedMigrations.Load()).RunAsync(CancellationToken.None);
        logging.GetRequiredService<ILogger<MigrationRunner>>().Write(result);
        return HostLaunch.Migrated(result.Outcome);
    }

    /// <summary>Opens the file destination and the startup container, runs the checks and hands both to the host when every one passed; otherwise closes them here.</summary>
    internal static async Task<HostLaunch> LaunchAsync(ConfigurationLoadResult loaded, DatabaseSettings database, TimeSpan analyzeTimeout, StartupReport target)
    {
        var fileLog = HostLogFile.Open(loaded);
        var startup = StartupServices(loaded, fileLog, database);
        try
        {
            IStartupCheck[] checks = [.. HostLogFile.Checks(loaded, fileLog), .. StartupChecks.Of(loaded, startup)];
            var report = await new StartupRunner(checks, startup.GetRequiredService<TimeProvider>()).RunAsync(CancellationToken.None);
            if (!report.Ready)
            {
                return HostLaunch.CheckFailed(report);
            }
            var launch = HostLaunch.Ready(await AssembleAsync(
                TlsConfiguration.Serve(Prepared(loaded, fileLog, analyzeTimeout, checks), startup),
                new StartupReport([.. target.Results, .. report.Results], report.CheckedAt),
                startup,
                RecognizerConfiguration.KindOf(loaded.Values)));
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

    /// <summary>Logging with redaction first, then the services the routes consume and a JSON that refuses a field unknown, missing or null; the container owns the file destination.</summary>
    internal static void Compose(IHostApplicationBuilder builder, EffectiveConfiguration configuration, ILoggerProvider? fileLog)
    {
        PlatformLogging.Configure(builder.Logging);
        _ = fileLog is null ? builder.Services : builder.Services.AddSingleton(_ => fileLog);
        _ = builder.Services.AddSingleton(TimeProvider.System).AddRedaction(PlatformLogging.EraseSensitiveData).AddSingleton(configuration);
        _ = builder.Services.ConfigureHttpJsonOptions(HostJson.Strict).AddRazorComponents();
    }

    /// <summary>The analysis and the recognizer the configuration chose: the fake by name, otherwise one per use, with the port and secret the supervisor has then.</summary>
    internal static void AddAnalysis(IServiceCollection services, IReadOnlyDictionary<string, string> values, TimeSpan analyzeTimeout)
    {
        _ = RecognizerConfiguration.UsesFake(values)
            ? services.AddSingleton<IRecognizer, FakeRecognizer>().AddSingleton<IServiceVersionReader, NoServiceVersionReader>()
            : services.AddTransient(provider => Recognizer(provider, analyzeTimeout))
                .AddTransient(ServiceVersionReader);
        _ = services.AddTransient<MaterialAnalysis>();
    }

    /// <summary>What the checks need before the host's container exists: redacted logging, the server checks and the language service's supervision, which the host then adopts.</summary>
    private static ServiceProvider StartupServices(ConfigurationLoadResult loaded, FileLoggerProvider? fileLog, DatabaseSettings database)
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<IMasterKeySource>(MasterKeyConfiguration.SourceOf(loaded.Values));
        _ = services.AddSingleton<IStartupCheck>(provider => new MasterKeyCheck(provider.GetRequiredService<IMasterKeySource>()));
        _ = services.AddSingleton(_ => new TlsMaterialCheck(TlsConfiguration.SourceOf(loaded.Values)))
            .AddSingleton<IStartupCheck>(provider => provider.GetRequiredService<TlsMaterialCheck>());
        AddServerGuarantees(services, database);
        _ = services.AddSingleton<IStartupCheck>(_ => new ServerLogCheck(AuditConfiguration.ServerLogDirectory(loaded.Values)));
        _ = services.AddSingleton(TimeProvider.System).AddLogging(logging => HostLogFile.WithFile(logging, fileLog)).AddRedaction(PlatformLogging.EraseSensitiveData);
        _ = fileLog is null ? services : services.AddSingleton(fileLog);
        _ = services.AddSingleton(_ => LoopbackHttpClient.Create());
        _ = services.AddSingleton(provider => new HealthWait(provider.GetRequiredService<HttpClient>()));
        _ = services.AddSingleton(new ServiceProcessLauncher(ServiceFolder.Beside().Executable(LanguageServiceConfiguration.GivenExecutable(loaded.Values))));
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

    /// <summary>The keys of the epochs file over a source that was read, and the rotation of the rows over them: the mode that rotates is not a root, and the services come from here.</summary>
    internal static ItemKeys KeysOf(MasterKeyReading reading) => new(new KeyDerivation(reading.Key!, reading.Labels), reading.Epochs);
    internal static EpochRotation RotationOf(NpgsqlDataSource dataSource, ItemKeys keys) => new(dataSource, keys);

    /// <summary>The rehearsal of the recovery over a source that was read, opening content with the labels the database registers.</summary>
    internal static RecoveryRehearsal RehearsalOf(NpgsqlDataSource dataSource, MasterKeyReading reading, IReadOnlySet<string> registered) =>
        new(dataSource, new ProtectedContent(reading.Labels, KeysOf(reading), registered), TimeProvider.System);

    /// <summary>The record of the rehearsal in the chain of the production, and the load of a restored copy.</summary>
    internal static RecoveryRecording RecordingOf(NpgsqlDataSource dataSource) => new(dataSource);
    internal static RestoreLoader LoaderOf(NpgsqlDataSource dataSource) => new(dataSource, new GuaranteeVerifier(dataSource), TimeProvider.System);

    /// <summary>Redacted console logging alone: the modes that migrate or rotate have no host, no file destination and no language service.</summary>
    internal static ServiceProvider LoggingServices() =>
        new ServiceCollection().AddSingleton(TimeProvider.System).AddLogging(PlatformLogging.Configure).AddRedaction(PlatformLogging.EraseSensitiveData).BuildServiceProvider();

    /// <summary>The application's data source, the verifier of the guarantees and the ten server checks, privilege first, the window of the owner credential second and the labels last.</summary>
    private static void AddServerGuarantees(IServiceCollection services, DatabaseSettings database)
    {
        _ = services.AddSingleton(_ => NpgsqlDataSource.Create(database.ConnectionString!));
        _ = services.AddSingleton<IStartupCheck>(provider => new PrivilegeCheck(provider.GetRequiredService<NpgsqlDataSource>()));
        _ = services.AddSingleton(provider => new GuaranteeVerifier(
            provider.GetRequiredService<NpgsqlDataSource>(), provider.GetRequiredService<IMasterKeySource>().ReadAsync,
            reading => ContentReader(provider.GetRequiredService<NpgsqlDataSource>(), reading))).AddSingleton<IStartupCheck, OwnerWindowCheck>();
        _ = services.AddSingleton<IStartupCheck>(provider => new RlsForceCheck(provider.GetRequiredService<NpgsqlDataSource>())).AddSingleton<IStartupCheck, RestoreWindowCheck>();
        _ = services.AddSingleton<IStartupCheck>(provider => new SerializableCheck(provider.GetRequiredService<NpgsqlDataSource>()));
        _ = services.AddSingleton<IStartupCheck>(provider => new PgAuditCheck(provider.GetRequiredService<NpgsqlDataSource>()));
        _ = services.AddSingleton<IStartupCheck>(provider => new ColumnRevokeCheck(provider.GetRequiredService<NpgsqlDataSource>()));
        _ = services.AddSingleton<IStartupCheck>(provider => new SchemaVersionCheck(provider.GetRequiredService<NpgsqlDataSource>(), EmbeddedMigrations.Load()));
        _ = services.AddSingleton<IStartupCheck>(provider => new ImmutabilityCheck(provider.GetRequiredService<NpgsqlDataSource>()));
        _ = services.AddSingleton<IStartupCheck>(provider => new ProtectedColumnCheck(provider.GetRequiredService<NpgsqlDataSource>()));
        _ = services.AddSingleton<IStartupCheck>(provider => new DerivationLabelCheck(provider.GetRequiredService<NpgsqlDataSource>(), provider.GetRequiredService<IMasterKeySource>()));
        _ = services.AddSingleton<IStartupCheck>(provider => new ContentEpochCheck(provider.GetRequiredService<NpgsqlDataSource>(), provider.GetRequiredService<IMasterKeySource>()));
    }

    /// <summary>The web host with everything the configuration decides: its address, logging, the file destination, the recognizer and the versions the checks confirmed.</summary>
    private static WebApplicationBuilder Prepared(ConfigurationLoadResult loaded, ILoggerProvider? fileLog, TimeSpan analyzeTimeout, System.Collections.IEnumerable checks)
    {
        var builder = ClosedBuilder(loaded.Values[HostConfiguration.UrlsPath]);
        _ = builder.Services.Configure<CookiePolicyOptions>(TransportSecurity.CookiePolicy);
        Compose(builder, loaded.View, fileLog);
        AddAnalysis(builder.Services, loaded.Values, analyzeTimeout);
        _ = builder.Services.AddSingleton(PasswordConfiguration.Derivation(loaded.Values)).AddSingleton(VerificationConfiguration.Schedule(loaded.Values));
        _ = builder.Services.AddSingleton(SealConfiguration.Declaration(loaded.Values))
            .AddSingleton(provider => Submissions.SubmissionParts.BatchesOf(provider, loaded.Values))
            .AddSingleton(Submissions.SubmissionParts.RegisterOf);
        _ = builder.Services.AddSingleton(_ => new ServerAuditLog(AuditConfiguration.ServerLogDirectory(loaded.Values))).AddSingleton<AuditComparisonState>();
        _ = builder.Services.AddHostedService(provider => new AuditComparisonService(
            new AuditComparisonPlan(
                AuditConfiguration.CompareInterval(loaded.Values),
                (from, to, token) => new AuditComparer(
                    provider.GetRequiredService<ServiceProvider>().GetRequiredService<NpgsqlDataSource>(), AuditConfiguration.ServerLogDirectory(loaded.Values))
                    .CompareAsync(from, to, token),
                provider.GetRequiredService<ILogger<AuditComparisonService>>()),
            provider.GetRequiredService<AuditComparisonState>(),
            provider.GetRequiredService<TimeProvider>()));
        _ = builder.Services.AddSingleton(provider => new SessionStore(SessionConfiguration.Lifetimes(loaded.Values), provider.GetRequiredService<TimeProvider>()));
        _ = builder.Services.AddSingleton(InstallationConfiguration.Of(loaded.Values)).AddSingleton(provider => InstallationConfiguration.Shown(StartupVersions.Of(checks), provider));
        _ = SafekeepingConfiguration.Declaration(loaded.Values) is { } declaration ? builder.Services.AddSingleton(declaration) : builder.Services;
        return builder;
    }

    /// <summary>What the checks produced, adopted by the host: the report, the startup container, the recognizer in force and, once, the service's versions for the startup line.</summary>
    private static async Task<WebApplication> AssembleAsync(WebApplicationBuilder builder, StartupReport report, ServiceProvider startup, RecognizerKind recognizer)
    {
        _ = builder.Services.AddSingleton(report).AddSingleton(_ => startup);
        _ = builder.Services.AddSingleton(provider => provider.GetRequiredService<ServiceProvider>().GetRequiredService<ServiceProcessWatch>());
        _ = builder.Services.AddSingleton(provider => provider.GetRequiredService<ServiceProvider>().GetService<GuaranteeVerifier>()!).AddSingleton<SealWarningState>();
        _ = builder.Services.AddSingleton(provider => new ChainVerificationService(provider)).AddHostedService(provider => provider.GetRequiredService<ChainVerificationService>());
        _ = builder.Services.AddHostedService(provider => new SealSendingService(provider));
        _ = builder.Services.AddHostedService(provider => new ConfigurationInForce(provider));
        _ = builder.Services.AddSingleton(provider => provider.GetRequiredService<ServiceProvider>().GetService<NpgsqlDataSource>() is { } source ? new DomainListing(source) : null!);
        _ = builder.Services.AddSingleton(provider => provider.GetRequiredService<ServiceProvider>().GetService<NpgsqlDataSource>() is { } source ? new OperatorAdministration(
            new PersonRegistry(source), CredentialsOf(source, provider.GetRequiredService<PasswordDerivation>()), provider.GetRequiredService<SessionStore>(), new PersonNotices(source)) : null!);
        _ = builder.Services.AddSingleton(provider => new SignInDelay(provider.GetRequiredService<TimeProvider>()));
        _ = builder.Services.AddSingleton(provider => provider.GetService<OperatorAdministration>() is { } staff
            ? new PasswordChange(staff.Credentials, provider.GetRequiredService<SessionStore>(), provider.GetRequiredService<SignInDelay>()) : null!);
        _ = builder.Services.AddSingleton(provider => provider.GetService<OperatorAdministration>() is { } people
            ? new SignIn(people.Registry, people.Credentials, provider.GetRequiredService<SessionStore>(), provider.GetRequiredService<SignInDelay>(),
                new SignInRecord(new SignInAttempts(provider.GetRequiredService<ServiceProvider>().GetRequiredService<NpgsqlDataSource>()), provider.GetRequiredService<ILoggerFactory>()))
            : null!);

        var app = builder.Build();
        _ = app.Services.GetRequiredService<ServiceProvider>();
        await StartupVersions.WriteAsync(app.Logger, app.Services.GetRequiredService<VersionReport>(), app.Services.GetRequiredService<IServiceVersionReader>(), CancellationToken.None);
        InstallationConfiguration.Announce(app.Logger, app.Services);
        HostRoutes.Map(app, recognizer);
        return app;
    }

    /// <summary>The store of passwords, built here because it is a service and only the root builds services.</summary>
    internal static PersonCredentials CredentialsOf(NpgsqlDataSource source, PasswordDerivation derivation) => new(source, derivation);

    /// <summary>The path that reads protected content over the key a reading holds, which the conference of the guarantees opens its sample with and lets go of.</summary>
    private static ProtectedContentReader ContentReader(NpgsqlDataSource source, MasterKeyReading reading) =>
        new(source, new ProtectedContent(reading.Labels, KeysOf(reading), new HashSet<string>()), new CustodyChain(source));

    /// <summary>The recognizer of the service the supervisor has alive now, with its secret and the client that never goes through a proxy;
    /// with no such service, one that refuses without connecting, so that nothing goes to the port a service that is gone once held.</summary>
    private static IRecognizer Recognizer(IServiceProvider host, TimeSpan analyzeTimeout)
    {
        var startup = host.GetRequiredService<ServiceProvider>();
        return startup.GetRequiredService<LanguageServiceSupervisor>().Endpoint is { } endpoint
            ? new HttpRecognizer(
                startup.GetRequiredService<HttpClient>(),
                new HttpRecognizerSettings(endpoint.Port, endpoint.Secret, analyzeTimeout),
                host.GetRequiredService<ILogger<HttpRecognizer>>())
            : new StoppedServiceRecognizer(host.GetRequiredService<ILogger<StoppedServiceRecognizer>>());
    }

    /// <summary>The reader of the versions of the service the supervisor has now; with no service running there is none to ask.</summary>
    private static IServiceVersionReader ServiceVersionReader(IServiceProvider host)
    {
        var startup = host.GetRequiredService<ServiceProvider>();
        return startup.GetRequiredService<LanguageServiceSupervisor>().Endpoint is { } endpoint
            ? new HttpServiceVersionReader(startup.GetRequiredService<HttpClient>(), new HttpRecognizerSettings(endpoint.Port, endpoint.Secret, LanguageServiceConfiguration.VersionsTimeout))
            : new NoServiceVersionReader();
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
