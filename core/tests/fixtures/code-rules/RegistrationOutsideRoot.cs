using Microsoft.Extensions.DependencyInjection;

namespace Fixtures;

internal static class StatusRegistration
{
    public static void AddStatus(IServiceCollection services)
    {
        _ = services.AddSingleton<StatusClock>();
    }
}

internal sealed class StatusClock;
