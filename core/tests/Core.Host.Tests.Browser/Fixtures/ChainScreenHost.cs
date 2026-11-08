using Core.Host.Endpoints;
using Core.Persistence.Tests.Fixtures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Core.Host.Tests.Browser.Fixtures;

/// <summary>
/// The host of the screens of the chain (issue 605) with the real routes, the real verification and a real chain, over the events the four people of the run left in it: with the chain
/// intact, or with the second event changed outside the system, which the verification of the host then finds and records as the first that does not conform. In both, one verification
/// is on record before the first request, made by the system and not by a person.
/// </summary>
internal sealed class ChainScreenHost : ScreenHostBase
{
    /// <summary>The sequence of the event the broken chain has changed, which the verification names as the first that does not conform.</summary>
    public const int BrokenSequence = 2;

    private const string Untrusted = "ALTER TABLE public.custody_event DISABLE TRIGGER custody_event_never_changed; ";
    private const string Trusted = "; ALTER TABLE public.custody_event ENABLE ALWAYS TRIGGER custody_event_never_changed";

    private readonly bool broken;

    private ChainScreenHost(ScratchDatabase database, bool broken)
        : base(database)
    {
        this.broken = broken;
    }

    public static Task<ChainScreenHost> StartAsync(bool broken, CancellationToken cancellationToken) =>
        StartAsync(database => new ChainScreenHost(database, broken), _ => Task.CompletedTask, host => host.RecordAsync(cancellationToken), cancellationToken);

    protected override void AddServices(IServiceCollection services)
    {
        var startup = new ServiceCollection().AddSingleton(_ => NpgsqlDataSource.Create(Database.AppConnectionString)).BuildServiceProvider();
        _ = services.AddSingleton(startup)
            .AddSingleton(new VerificationSchedule(TimeSpan.FromHours(1)))
            .AddSingleton(new SealDeclaration(SealSituation.External, null))
            .AddSingleton<SealWarningState>()
            .AddSingleton(provider => new ChainVerificationService(provider));
    }

    protected override void MapScreens(IEndpointRouteBuilder routes) => ChainScreensEndpoint.Map(routes);

    /// <summary>The first verification on record: of the chain as the run left it, or of the chain after the change outside the system.</summary>
    private async Task RecordAsync(CancellationToken cancellationToken)
    {
        if (broken)
        {
            await Database.ExecuteAsSuperuserAsync($"{Untrusted}UPDATE public.custody_event SET item_id = item_id + 1 WHERE sequence = {BrokenSequence}{Trusted}");
        }

        var verified = await Services.GetRequiredService<ChainVerificationService>().VerifyAsync(seal: null, requestedBy: null, cancellationToken);
        Assert.NotNull(verified);
        Assert.Equal(!broken, verified.Passed);
    }
}
