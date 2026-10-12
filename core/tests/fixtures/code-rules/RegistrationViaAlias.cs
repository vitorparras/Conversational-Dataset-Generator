using Microsoft.Extensions.DependencyInjection;

namespace Fixtures;

internal static class AliasedRegistration
{
    public static void Register(WebApplicationBuilder builder)
    {
        var collection = builder.Services;
        _ = collection.AddSingleton<StatusClock>();
    }
}

internal sealed class StatusClock;
