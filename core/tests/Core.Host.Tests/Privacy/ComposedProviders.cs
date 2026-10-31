using System.Reflection;
using Core.Host.Supervision;
using Core.Host.Tests.Logging;
using Core.Persistence.Connection;
using Core.Platform.Configuration;
using Core.Platform.Configuration.Sources;
using Core.Platform.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Core.Host.Tests.Privacy;

/// <summary>
/// The type of every logger provider the host holds, in both of its logging containers: the web one, which the routes
/// log through, and the startup one, which the supervision of the language service logs through. The startup one is
/// private to the composition root, so it is reached by name, and a rename fails the tests instead of hiding it.
/// </summary>
internal static class ComposedProviders
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Static;

    /// <summary>The types of the web container, after <paramref name="further"/> has had its say on the logging builder.</summary>
    public static IReadOnlyList<Type> WebTypesOf(Action<ILoggingBuilder> further)
    {
        using var file = new OpenedLogFile();
        using var app = ComposedHost.Build(file.Provider, further);
        return TypesIn(app.Services);
    }

    /// <summary>The types of the startup container, built by the composition root's own method from the configuration of a real start.</summary>
    public static IReadOnlyList<Type> StartupTypes()
    {
        using var file = new OpenedLogFile();
        var loaded = ClosedConfiguration.Load(
            new ConfigurationSchema([
                HostConfiguration.Section, LanguageServiceConfiguration.Section, RecognizerConfiguration.Section, DatabaseConfiguration.Section,
                MasterKeyConfiguration.Section]),
            new StrictArgumentsSource(
                ["--LanguageService:Executable=zqx-never-started-7731", "--MasterKey:File=zqx-never-read-7731", "--MasterKey:LabelsFile=zqx-never-read-7732"]));
        Assert.False(loaded.Refused, "The configuration of a start with only the interpreter named must load.");
        var startup = (ServiceProvider)Method("StartupServices").Invoke(null, [loaded, file.Provider, DatabaseConfiguration.Read(loaded.Values)])!;
        using (startup)
        {
            return TypesIn(startup);
        }
    }

    /// <summary>The types the startup container's logging holds when <paramref name="further"/> adds to what the host's own method for it registers.</summary>
    public static IReadOnlyList<Type> StartupTypesWith(Action<ILoggingBuilder> further)
    {
        using var file = new OpenedLogFile();
        var services = new ServiceCollection().AddSingleton(TimeProvider.System);
        _ = services.AddLogging(logging =>
        {
            HostLogFile.WithFile(logging, file.Provider);
            further(logging);
        });
        using var provider = services.BuildServiceProvider();
        return TypesIn(provider);
    }

    private static List<Type> TypesIn(IServiceProvider services) =>
        [.. services.GetServices<ILoggerProvider>().Select(provider => provider.GetType())];

    private static MethodInfo Method(string name) =>
        typeof(CompositionRoot).GetMethod(name, Private) ?? throw new InvalidOperationException($"CompositionRoot.{name} was not found; the privacy test must follow it.");

    private sealed class OpenedLogFile : IDisposable
    {
        private readonly TemporaryDirectory directory = new();

        public OpenedLogFile()
        {
            Provider = FileLoggerProvider.Open(Path.Combine(directory.Path, "ZQX-LOG-0028.log"), new LineConsoleFormatter(TimeProvider.System), TextWriter.Null);
        }

        public FileLoggerProvider? Provider { get; }

        public void Dispose()
        {
            Provider?.Dispose();
            directory.Dispose();
        }
    }
}
