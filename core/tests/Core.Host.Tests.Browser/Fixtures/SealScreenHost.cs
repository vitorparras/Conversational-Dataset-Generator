using Core.Host.Endpoints;
using Core.Persistence.Tests.Fixtures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Core.Host.Tests.Browser.Fixtures;

/// <summary>
/// The host of the screen of the seal (issue 736) with the real routes, the real chain and the real sealer, over the events the four people of the run left in it, in the manual situation or
/// in the one where the host sends the seal itself. No seal is on record before the first request.
/// </summary>
internal sealed class SealScreenHost : ScreenHostBase
{
    private readonly bool manual;

    private SealScreenHost(ScratchDatabase database, bool manual)
        : base(database)
    {
        this.manual = manual;
    }

    public static Task<SealScreenHost> StartAsync(bool manual, CancellationToken cancellationToken) =>
        StartAsync(database => new SealScreenHost(database, manual), _ => Task.CompletedTask, cancellationToken);

    protected override void AddServices(IServiceCollection services)
    {
        var startup = new ServiceCollection().AddSingleton(_ => NpgsqlDataSource.Create(Database.AppConnectionString)).BuildServiceProvider();
        var situation = manual ? new SealDeclaration(SealSituation.Manual, null) : new SealDeclaration(SealSituation.External, new Uri("https://selo.example.test/"));
        _ = services.AddSingleton(startup)
            .AddSingleton(new VerificationSchedule(TimeSpan.FromHours(1)))
            .AddSingleton(situation)
            .AddSingleton<SealWarningState>()
            .AddSingleton(provider => new ChainVerificationService(provider));
    }

    protected override void MapScreens(IEndpointRouteBuilder routes)
    {
        ChainScreensEndpoint.Map(routes);
        SealScreensEndpoint.Map(routes);
    }
}
