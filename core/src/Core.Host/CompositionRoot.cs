using Core.Host.Endpoints;

namespace Core.Host;

/// <summary>The only place where the host is assembled: services, then routes.</summary>
internal static class CompositionRoot
{
    public static WebApplication Build(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        var app = builder.Build();
        StatusEndpoint.Map(app);
        return app;
    }
}
