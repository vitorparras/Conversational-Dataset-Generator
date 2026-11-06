using Core.Host.Endpoints;
using Core.Persistence.Domains;
using Core.Persistence.Tests.Fixtures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Core.Host.Tests.Browser.Fixtures;

/// <summary>
/// The host of the screen of the domains (issue 602) with the real route: with the two domains of <see cref="Domains"/> made up for the run, or with none, as the test asks.
/// </summary>
internal sealed class DomainScreenHost : ScreenHostBase
{
    /// <summary>The names of the domains of the run, in the order they were created.</summary>
    public static readonly string[] Domains = ["ZQX-dominio-alfa", "ZQX-dominio-beta"];

    private readonly bool withDomains;

    private DomainScreenHost(ScratchDatabase database, bool withDomains)
        : base(database)
    {
        this.withDomains = withDomains;
    }

    public static Task<DomainScreenHost> StartAsync(bool withDomains, CancellationToken cancellationToken) =>
        StartAsync(database => new DomainScreenHost(database, withDomains), host => host.SeedAsync(), cancellationToken);

    protected override void AddServices(IServiceCollection services) =>
        _ = services.AddSingleton(provider => new DomainListing(provider.GetRequiredService<NpgsqlDataSource>()));

    protected override void MapScreens(IEndpointRouteBuilder routes) => DomainScreensEndpoint.Map(routes);

    private async Task SeedAsync()
    {
        if (withDomains)
        {
            await Database.RunAsOwnerAsync($"INSERT INTO public.domain (name) VALUES ('{Domains[0]}'), ('{Domains[1]}')");
        }
    }
}
