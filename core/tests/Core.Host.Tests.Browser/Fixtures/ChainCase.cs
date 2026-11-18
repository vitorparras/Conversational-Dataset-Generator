using Core.Host.Endpoints;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Core.Host.Tests.Browser.Fixtures;

/// <summary>
/// The case of the screens of the chain (issue 605) with the real routes, the real verification and a real chain, over the events the four people of the run left in it: with the chain
/// intact, or with the second event changed outside the system, which the verification of the host then finds and records as the first that does not conform. In both, one verification
/// is on record before the first request, made by the system and not by a person.
/// </summary>
internal static class ChainCase
{
    /// <summary>The sequence of the event the broken chain has changed, which the verification names as the first that does not conform.</summary>
    public const int BrokenSequence = 2;

    private const string Untrusted = "ALTER TABLE public.custody_event DISABLE TRIGGER custody_event_never_changed; ";
    private const string Trusted = "; ALTER TABLE public.custody_event ENABLE ALWAYS TRIGGER custody_event_never_changed";

    public static ScreenParts Parts(bool broken, CancellationToken cancellationToken) => new()
    {
        Services = (services, setup) =>
        {
            var startup = new ServiceCollection().AddSingleton(_ => NpgsqlDataSource.Create(setup.Database.AppConnectionString)).BuildServiceProvider();
            _ = services.AddSingleton(startup)
                .AddSingleton(new VerificationSchedule(TimeSpan.FromHours(1)))
                .AddSingleton(new SealDeclaration(SealSituation.External, null))
                .AddSingleton<SealWarningState>()
                .AddSingleton(provider => new ChainVerificationService(provider));
        },
        Screens = ChainScreensEndpoint.Map,
        Finish = host => RecordAsync(host, broken, cancellationToken),
    };

    /// <summary>The first verification on record: of the chain as the run left it, or of the chain after the change outside the system.</summary>
    private static async Task RecordAsync(ScreenHost host, bool broken, CancellationToken cancellationToken)
    {
        if (broken)
        {
            await host.Database.ExecuteAsSuperuserAsync($"{Untrusted}UPDATE public.custody_event SET item_id = item_id + 1 WHERE sequence = {BrokenSequence}{Trusted}");
        }

        var verified = await host.Services.GetRequiredService<ChainVerificationService>().VerifyAsync(seal: null, requestedBy: null, cancellationToken);
        Assert.NotNull(verified);
        Assert.Equal(!broken, verified.Passed);
    }
}
