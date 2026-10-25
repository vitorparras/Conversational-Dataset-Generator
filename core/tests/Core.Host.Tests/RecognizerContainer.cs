using Core.Host.Supervision;
using Core.Platform.Configuration;
using Core.Platform.Configuration.Sources;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Host.Tests;

/// <summary>The recognizer key loaded from arguments, and a container holding the recognizer the composition root registers for it.</summary>
internal static class RecognizerContainer
{
    public static ConfigurationLoadResult Loaded(params string[] arguments) =>
        ClosedConfiguration.Load(new ConfigurationSchema([RecognizerConfiguration.Section]), new StrictArgumentsSource(arguments));

    /// <summary>The host's services around the recognizer, with the supervisor and the client the host's startup services would hold.</summary>
    public static ServiceProvider Of(IReadOnlyDictionary<string, string> values, LanguageServiceSupervisor supervisor, HttpClient client)
    {
        var startup = new ServiceCollection().AddSingleton(client).AddSingleton(supervisor).BuildServiceProvider();
        var services = new ServiceCollection().AddLogging().AddSingleton(_ => startup);
        CompositionRoot.AddAnalysis(services, values, TimeSpan.FromSeconds(30));
        return services.BuildServiceProvider();
    }
}
