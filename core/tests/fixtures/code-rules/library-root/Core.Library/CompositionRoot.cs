using Microsoft.Extensions.DependencyInjection;

namespace Core.Library;

internal static class CompositionRoot
{
    public static void AddLibrary(IServiceCollection services)
    {
        _ = services.AddSingleton<LibraryClock>();
    }
}

internal sealed class LibraryClock;
