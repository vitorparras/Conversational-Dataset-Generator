using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Core.Host.Endpoints;
using Core.Persistence.Domains;
using Core.Persistence.People;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Core.Host.Tests.Browser.Fixtures;

/// <summary>
/// The case of the notices (issue 595) with the real routes: the list of the notices, the screen of the domains, which says when the writing is suspended, and the route that sets a password
/// again, so that the notice of a password reset is made by the act and not written by the test. Over the real verification of the chain, with the chain intact or with the second event
/// changed outside the system, which the verification of the host finds and, failing, tells every active person. People made for one test, <see cref="AddPersonAsync"/>, keep each test
/// away from the notices of another.
/// </summary>
internal sealed class NoticeCase : IAsyncDisposable
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
    private readonly PersonPool pool;

    private NoticeCase(ScreenHost host, bool broken, PersonPool pool)
    {
        Host = host;
        this.broken = broken;
        this.pool = pool;
    }

    /// <summary>The host of the case.</summary>
    public ScreenHost Host { get; }

    [SuppressMessage("Reliability", "CA2000", Justification = "The host goes to the case that is returned, which disposes it.")]
    public static async Task<NoticeCase> StartAsync(bool broken, CancellationToken cancellationToken)
    {
        var pool = new PersonPool();
        var host = await ScreenHost.StartAsync(
            new ScreenParts
            {
                Services = (services, setup) =>
                {
                    var startup = new ServiceCollection().AddSingleton(_ => NpgsqlDataSource.Create(setup.Database.AppConnectionString)).BuildServiceProvider();
                    _ = services.AddSingleton(startup)
                        .AddSingleton(new VerificationSchedule(TimeSpan.FromHours(1)))
                        .AddSingleton(new SealDeclaration(SealSituation.External, null))
                        .AddSingleton<SealWarningState>()
                        .AddSingleton(provider => new ChainVerificationService(provider))
                        .AddSingleton(provider => new DomainListing(provider.GetRequiredService<NpgsqlDataSource>()));
                },
                Screens = routes =>
                {
                    NoticeScreensEndpoint.Map(routes);
                    DomainScreensEndpoint.Map(routes);
                    OperatorsEndpoint.Map(routes);
                },
                Prepare = database => database.RunAsOwnerAsync($"INSERT INTO public.domain (name) VALUES ('{Domains[0]}'), ('{Domains[1]}')"),
                Finish = started => RecordAsync(started, broken, pool, cancellationToken),
            },
            cancellationToken);
        return new NoticeCase(host, broken, pool);
    }

    /// <summary>
    /// A person of the function given, made for one test, with the password of the run and no notice but the one the facts of the host tell them. Over the chain that conforms the person is made
    /// now; over the broken one, which accepts no new person, the next of those made before the break is given, and each already has the notice that the writing is suspended.
    /// </summary>
    public async Task<PersonOfTheRun> AddPersonAsync(string function, CancellationToken cancellationToken)
    {
        if (broken)
        {
            return pool.Take(function);
        }

        return await MakePersonAsync(Host, function, cancellationToken);
    }

    /// <summary>Tells the person that their password was set again and how many sessions that ended, as the act does, for the tests that read the list and not the act.</summary>
    public async Task TellPasswordResetAsync(long personId, int sessionsEnded, CancellationToken cancellationToken)
    {
        var source = NpgsqlDataSource.Create(Host.Database.AppConnectionString);
        await using (source.ConfigureAwait(false))
        {
            await new PersonNotices(source).RecordPasswordResetAsync(personId, sessionsEnded, cancellationToken);
        }
    }

    /// <summary>The verification of the host once more, by the system, as the periodic one is: on a broken chain it fails again and tells whoever is not told yet.</summary>
    public Task VerifyAsync(CancellationToken cancellationToken) => VerifyAsync(Host, broken, cancellationToken);

    /// <summary>How many notices the person has on record and how many of them were read, which the database keeps whatever the list shows.</summary>
    public async Task<(int Recorded, int Read)> RecordOfAsync(long personId)
    {
        var row = Assert.Single(await Host.Database.FirstColumnAsOwnerAsync($"SELECT count(*) || ',' || count(read_at) FROM public.person_notice WHERE person_id = {personId}"));
        var parts = row.Split(',');
        return (int.Parse(parts[0], CultureInfo.InvariantCulture), int.Parse(parts[1], CultureInfo.InvariantCulture));
    }

    /// <summary>The identifiers of the unread notices of the person, the newest first.</summary>
    public async Task<IReadOnlyList<long>> UnreadIdsOfAsync(long personId) =>
        [.. (await Host.Database.FirstColumnAsOwnerAsync($"SELECT id::text FROM public.person_notice WHERE person_id = {personId} AND read_at IS NULL ORDER BY id DESC"))
            .Select(id => long.Parse(id, CultureInfo.InvariantCulture))];

    public ValueTask DisposeAsync() => Host.DisposeAsync();

    private static async Task VerifyAsync(ScreenHost host, bool broken, CancellationToken cancellationToken)
    {
        var verified = await host.Services.GetRequiredService<ChainVerificationService>().VerifyAsync(seal: null, requestedBy: null, cancellationToken);
        Assert.NotNull(verified);
        Assert.Equal(!broken, verified.Passed);
    }

    /// <summary>
    /// The first verification on record: of the chain as the run left it, or of the chain after the change outside the system, once the people the tests will need are made, since a chain that does
    /// not conform accepts no new person.
    /// </summary>
    private static async Task RecordAsync(ScreenHost host, bool broken, PersonPool pool, CancellationToken cancellationToken)
    {
        if (broken)
        {
            await MakePoolAsync(host, pool, cancellationToken);
            await host.Database.ExecuteAsSuperuserAsync($"{Untrusted}UPDATE public.custody_event SET item_id = item_id + 1 WHERE sequence = {BrokenSequence}{Trusted}");
        }

        await VerifyAsync(host, broken, cancellationToken);
    }

    private static async Task MakePoolAsync(ScreenHost host, PersonPool pool, CancellationToken cancellationToken)
    {
        foreach (var function in new[] { PersonFunctions.Audit, PersonFunctions.Accepted[0] })
        {
            var made = new Queue<PersonOfTheRun>();
            for (var person = 0; person < PoolPerFunction; person++)
            {
                made.Enqueue(await MakePersonAsync(host, function, cancellationToken));
            }

            pool.Keep(function, made);
        }
    }

    private static async Task<PersonOfTheRun> MakePersonAsync(ScreenHost host, string function, CancellationToken cancellationToken)
    {
        var name = $"zqx-aviso-{Guid.NewGuid():N}";
        return new PersonOfTheRun(name, await host.AddPersonAsync(name, function, cancellationToken));
    }

    /// <summary>The people made before the chain is broken, which the tests take one by one, by function.</summary>
    private sealed class PersonPool
    {
        private readonly Dictionary<string, Queue<PersonOfTheRun>> made = [];

        public PersonOfTheRun Take(string function) => made[function].Dequeue();

        public void Keep(string function, Queue<PersonOfTheRun> people) => made[function] = people;
    }

    /// <summary>A person made for one test: the name they enter with and the identifier the database gave them.</summary>
    internal sealed record PersonOfTheRun(string Name, long Id);
}
