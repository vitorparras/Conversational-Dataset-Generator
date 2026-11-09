using System.Globalization;
using Core.Host.Endpoints;
using Core.Persistence.Custody;
using Core.Persistence.Domains;
using Core.Persistence.People;
using Core.Persistence.Tests.Fixtures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Core.Host.Tests.Browser.Fixtures;

/// <summary>
/// The host of the notices (issue 595) with the real routes: the list of the notices, the screen of the domains, which says when the writing is suspended, and the route that sets a password
/// again, so that the notice of a password reset is made by the act and not written by the test. Over the real verification of the chain, with the chain intact or with the second event
/// changed outside the system, which the verification of the host finds and, failing, tells every active person. People made for one test, <see cref="AddPersonAsync"/>, keep each test
/// away from the notices of another.
/// </summary>
internal sealed class NoticeScreenHost : ScreenHostBase
{
    /// <summary>The names of the domains of the run, which the screen of the domains lists.</summary>
    public static readonly string[] Domains = ["ZQX-dominio-alfa", "ZQX-dominio-beta"];

    /// <summary>The sequence of the event the broken chain has changed, which no notice may name.</summary>
    public const int BrokenSequence = 2;

    private const string Untrusted = "ALTER TABLE public.custody_event DISABLE TRIGGER custody_event_never_changed; ";
    private const string Trusted = "; ALTER TABLE public.custody_event ENABLE ALWAYS TRIGGER custody_event_never_changed";

    /// <summary>How many people of each function are made before the chain is broken, since a chain that does not conform accepts no new person.</summary>
    private const int PoolPerFunction = 8;

    private readonly bool broken;
    private readonly Dictionary<string, Queue<PersonOfTheRun>> pool = [];

    private NoticeScreenHost(ScratchDatabase database, bool broken)
        : base(database)
    {
        this.broken = broken;
    }

    public static Task<NoticeScreenHost> StartAsync(bool broken, CancellationToken cancellationToken) =>
        StartAsync(
            database => new NoticeScreenHost(database, broken),
            host => host.Database.RunAsOwnerAsync($"INSERT INTO public.domain (name) VALUES ('{Domains[0]}'), ('{Domains[1]}')"),
            host => host.RecordAsync(cancellationToken),
            cancellationToken);

    /// <summary>
    /// A person of the function given, made for one test, with the password of the run and no notice but the one the facts of the host tell them. Over the chain that conforms the person is made
    /// now; over the broken one, which accepts no new person, the next of those made before the break is given, and each already has the notice that the writing is suspended.
    /// </summary>
    public async Task<PersonOfTheRun> AddPersonAsync(string function, CancellationToken cancellationToken)
    {
        if (broken)
        {
            return pool[function].Dequeue();
        }

        var source = NpgsqlDataSource.Create(Database.AppConnectionString);
        await using (source.ConfigureAwait(false))
        {
            return await MakePersonAsync(source, function, cancellationToken);
        }
    }

    /// <summary>Tells the person that their password was set again and how many sessions that ended, as the act does, for the tests that read the list and not the act.</summary>
    public async Task TellPasswordResetAsync(long personId, int sessionsEnded, CancellationToken cancellationToken)
    {
        var source = NpgsqlDataSource.Create(Database.AppConnectionString);
        await using (source.ConfigureAwait(false))
        {
            await new PersonNotices(source).RecordPasswordResetAsync(personId, sessionsEnded, cancellationToken);
        }
    }

    /// <summary>The verification of the host once more, by the system, as the periodic one is: on a broken chain it fails again and tells whoever is not told yet.</summary>
    public async Task VerifyAsync(CancellationToken cancellationToken)
    {
        var verified = await Services.GetRequiredService<ChainVerificationService>().VerifyAsync(seal: null, requestedBy: null, cancellationToken);
        Assert.NotNull(verified);
        Assert.Equal(!broken, verified.Passed);
    }

    /// <summary>How many notices the person has on record and how many of them were read, which the database keeps whatever the list shows.</summary>
    public async Task<(int Recorded, int Read)> RecordOfAsync(long personId)
    {
        var row = Assert.Single(await Database.FirstColumnAsOwnerAsync($"SELECT count(*) || ',' || count(read_at) FROM public.person_notice WHERE person_id = {personId}"));
        var parts = row.Split(',');
        return (int.Parse(parts[0], CultureInfo.InvariantCulture), int.Parse(parts[1], CultureInfo.InvariantCulture));
    }

    /// <summary>The identifiers of the unread notices of the person, the newest first.</summary>
    public async Task<IReadOnlyList<long>> UnreadIdsOfAsync(long personId) =>
        [.. (await Database.FirstColumnAsOwnerAsync($"SELECT id::text FROM public.person_notice WHERE person_id = {personId} AND read_at IS NULL ORDER BY id DESC"))
            .Select(id => long.Parse(id, CultureInfo.InvariantCulture))];

    protected override void AddServices(IServiceCollection services)
    {
        var startup = new ServiceCollection().AddSingleton(_ => NpgsqlDataSource.Create(Database.AppConnectionString)).BuildServiceProvider();
        _ = services.AddSingleton(startup)
            .AddSingleton(new VerificationSchedule(TimeSpan.FromHours(1)))
            .AddSingleton(new SealDeclaration(SealSituation.External, null))
            .AddSingleton<SealWarningState>()
            .AddSingleton(provider => new ChainVerificationService(provider))
            .AddSingleton(provider => new DomainListing(provider.GetRequiredService<NpgsqlDataSource>()));
    }

    protected override void MapScreens(IEndpointRouteBuilder routes)
    {
        NoticeScreensEndpoint.Map(routes);
        DomainScreensEndpoint.Map(routes);
        OperatorsEndpoint.Map(routes);
    }

    /// <summary>
    /// The first verification on record: of the chain as the run left it, or of the chain after the change outside the system, once the people the tests will need are made, since a chain that does
    /// not conform accepts no new person.
    /// </summary>
    private async Task RecordAsync(CancellationToken cancellationToken)
    {
        if (broken)
        {
            await MakePoolAsync(cancellationToken);
            await Database.ExecuteAsSuperuserAsync($"{Untrusted}UPDATE public.custody_event SET item_id = item_id + 1 WHERE sequence = {BrokenSequence}{Trusted}");
        }

        await VerifyAsync(cancellationToken);
    }

    private async Task MakePoolAsync(CancellationToken cancellationToken)
    {
        var source = NpgsqlDataSource.Create(Database.AppConnectionString);
        await using (source.ConfigureAwait(false))
        {
            foreach (var function in new[] { PersonFunctions.Audit, PersonFunctions.Accepted[0] })
            {
                var made = new Queue<PersonOfTheRun>();
                for (var person = 0; person < PoolPerFunction; person++)
                {
                    made.Enqueue(await MakePersonAsync(source, function, cancellationToken));
                }

                pool[function] = made;
            }
        }
    }

    private async Task<PersonOfTheRun> MakePersonAsync(NpgsqlDataSource source, string function, CancellationToken cancellationToken)
    {
        var name = $"zqx-aviso-{Guid.NewGuid():N}";
        var created = await new PersonRegistry(source).CreateAsync(new NewPerson(name, function), CustodyAuthor.System, cancellationToken);
        var id = created.PersonId!.Value;
        _ = await new PersonCredentials(source, new PasswordDerivation(1)).SetPasswordAsync(id, Password, CustodyAuthor.System, cancellationToken);
        return new PersonOfTheRun(name, id);
    }

    /// <summary>A person made for one test: the name they enter with and the identifier the database gave them.</summary>
    internal sealed record PersonOfTheRun(string Name, long Id);
}
