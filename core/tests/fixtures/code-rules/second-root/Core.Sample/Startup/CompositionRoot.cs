using Microsoft.Extensions.DependencyInjection;

namespace Core.Sample.Startup;

// A second file with the root's name, hoping to inherit its exemption.
internal static class CompositionRoot
{
    public static void AddChecks(IServiceCollection services)
    {
        _ = services.AddSingleton<SampleClock>();
    }
}
