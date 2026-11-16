using Core.Host.Endpoints;
using Core.Persistence.Domains;
using Core.Persistence.Ingestion;
using Core.Persistence.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Core.Host.Tests.Browser.Fixtures;

/// <summary>
/// The case of the screen of the batches (issue 608) with the real route over two domains with one batch each: with the first domain granted to everyone who enters, or with no grant
/// at all, which is the source of production before #112.
/// </summary>
internal static class BatchCase
{
    /// <summary>The domain granted, the first, whose batch is the batch 1.</summary>
    public const string Granted = "ZQX-dominio-lotes";

    /// <summary>The domain nobody is granted, whose batch is the batch 2.</summary>
    public const string Other = "ZQX-dominio-alheio";

    public static ScreenParts Parts(bool granting) => new()
    {
        Services = (services, _) => (granting ? services.AddSingleton<DomainGrants>(new FirstGranted()) : services)
            .AddSingleton(provider => new DomainListing(provider.GetRequiredService<NpgsqlDataSource>()))
            .AddSingleton(provider => new BatchRegister(provider.GetRequiredService<NpgsqlDataSource>())),
        Screens = BatchScreensEndpoint.Map,
        Prepare = async database =>
        {
            await database.RunAsOwnerAsync($"INSERT INTO public.domain (name) VALUES ('{Granted}'), ('{Other}')");
            await database.RunAsOwnerAsync(SeededBatches.Statements(1, 2));
        },
    };

    /// <summary>The first domain granted to whoever enters, whatever the function; the function still decides what the gate opens.</summary>
    private sealed class FirstGranted : DomainGrants
    {
        internal override Task<IEnumerable<string>> GrantedToAsync(long entered, CancellationToken cancellationToken) => Task.FromResult(Enumerable.Repeat(Granted, 1));
    }
}
