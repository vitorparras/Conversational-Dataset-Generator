using Core.Host.Endpoints;
using Core.Persistence.Domains;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Core.Host.Tests.Browser.Fixtures;

/// <summary>
/// The case of the screen of the domains (issue 602) with the real route: with the two domains of <see cref="Domains"/> made up for the run, or with none, as the test asks.
/// </summary>
internal static class DomainCase
{
    /// <summary>The names of the domains of the run, in the order they were created.</summary>
    public static readonly string[] Domains = ["ZQX-dominio-alfa", "ZQX-dominio-beta"];

    public static ScreenParts Parts(bool withDomains) => new()
    {
        Services = (services, _) => services.AddSingleton(provider => new DomainListing(provider.GetRequiredService<NpgsqlDataSource>())),
        Screens = DomainScreensEndpoint.Map,
        Prepare = async database =>
        {
            if (withDomains)
            {
                await database.RunAsOwnerAsync($"INSERT INTO public.domain (name) VALUES ('{Domains[0]}'), ('{Domains[1]}')");
            }
        },
    };
}
