using Core.Host.Endpoints;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Core.Host.Tests.Browser.Fixtures;

/// <summary>
/// The case of the screen of the seal (issue 736) with the real routes, the real chain and the real sealer, over the events the four people of the run left in it, in the manual situation or
/// in the one where the host sends the seal itself. No seal is on record before the first request.
/// </summary>
internal sealed class SealCase : IAsyncDisposable
{
    private readonly KeptLog keeper;

    private SealCase(ScreenHost host, KeptLog keeper)
    {
        Host = host;
        this.keeper = keeper;
    }

    /// <summary>The host of the case.</summary>
    public ScreenHost Host { get; }

    /// <summary>Every line the host logged, as text, so that a test can look for a seal in it.</summary>
    public string Log => keeper.Text;

    public static async Task<SealCase> StartAsync(bool manual, CancellationToken cancellationToken)
    {
        var keeper = new KeptLog();
        var host = await ScreenHost.StartAsync(
            new ScreenParts
            {
                Services = (services, setup) =>
                {
                    _ = services.AddLogging(logging => logging.AddProvider(keeper));
                    var startup = new ServiceCollection().AddSingleton(_ => NpgsqlDataSource.Create(setup.Database.AppConnectionString)).BuildServiceProvider();
                    var situation = manual ? new SealDeclaration(SealSituation.Manual, null) : new SealDeclaration(SealSituation.External, new Uri("https://selo.example.test/"));
                    _ = services.AddSingleton(startup)
                        .AddSingleton(new VerificationSchedule(TimeSpan.FromHours(1)))
                        .AddSingleton(situation)
                        .AddSingleton<SealWarningState>()
                        .AddSingleton(provider => new ChainVerificationService(provider));
                },
                Screens = routes =>
                {
                    ChainScreensEndpoint.Map(routes);
                    SealScreensEndpoint.Map(routes);
                },
            },
            cancellationToken);
        return new SealCase(host, keeper);
    }

    public ValueTask DisposeAsync() => Host.DisposeAsync();
}
