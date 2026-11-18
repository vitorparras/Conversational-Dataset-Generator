using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Core.Host.Endpoints;
using Core.Persistence.Content.Emptying;
using Core.Persistence.People;
using Core.Persistence.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Core.Host.Tests.Browser.Fixtures;

/// <summary>
/// The case of the screens of the emptying of an item (issue 617) with the real routes, the real desk and the real emptier, over a database that holds two domains, <see cref="Domain"/>, which the
/// curation has granted, and <see cref="OtherDomain"/>, which it has not, with <see cref="Items"/> items with content in the first and one in the second, put in by the owner outside the system.
/// Besides the four people of the run it has a person of the curation with no domain granted, <see cref="UngrantedCurator"/>. A test takes an item of its own with <see cref="NextItem"/>, because
/// an emptying cannot be undone.
/// </summary>
internal sealed class EmptyingCase : IAsyncDisposable
{
    /// <summary>The domain the curation has granted, which is the active one.</summary>
    public const string Domain = "zqx-dominio-a";

    /// <summary>The domain nobody has granted.</summary>
    public const string OtherDomain = "zqx-dominio-b";

    /// <summary>How many items with content the first domain has.</summary>
    public const int Items = 60;

    /// <summary>The item of the second domain, which the first cannot reach.</summary>
    public const int ItemOfOtherDomain = Items + 1;

    /// <summary>An identifier no item has.</summary>
    public const int MissingItem = 999_999;

    private int taken;

    private EmptyingCase(ScreenHost host, string ungrantedCurator)
    {
        Host = host;
        UngrantedCurator = ungrantedCurator;
    }

    /// <summary>The host of the case.</summary>
    public ScreenHost Host { get; }

    /// <summary>The person of the curation of the collection with no domain granted, who reaches nothing of the emptying.</summary>
    public string UngrantedCurator { get; }

    [SuppressMessage("Reliability", "CA2000", Justification = "The host goes to the case that is returned, which disposes it.")]
    public static async Task<EmptyingCase> StartAsync(CancellationToken cancellationToken)
    {
        var ungranted = $"zqx-curadoria-sem-dominio-{Guid.NewGuid():N}";
        var host = await ScreenHost.StartAsync(
            new ScreenParts
            {
                Services = (services, setup) =>
                    _ = services.AddSingleton<DomainGrants>(provider => new GrantedToTheCurator(provider.GetRequiredService<NpgsqlDataSource>(), setup.People.Curator))
                        .AddSingleton(provider => ItemEmptyingDesk.On(provider.GetRequiredService<NpgsqlDataSource>())),
                Screens = EmptyingScreensEndpoint.Map,
                Prepare = SeedAsync,
                Finish = started => started.AddPersonAsync(ungranted, PersonFunctions.Curation, cancellationToken),
            },
            cancellationToken);
        return new EmptyingCase(host, ungranted);
    }

    /// <summary>An item of the first domain no other test has taken, with content and not emptied.</summary>
    public int NextItem()
    {
        var item = Interlocked.Increment(ref taken);
        return item <= Items ? item : throw new InvalidOperationException("The host has no item left; seed more.");
    }

    /// <summary>Empties the item as the person of the curation, as the screen would, so that a test starts from an item that is emptied already.</summary>
    public async Task EmptyAsync(int item, EmptyingMotive motive, string protocol)
    {
        await using var source = NpgsqlDataSource.Create(Host.Database.AppConnectionString);
        var result = await ItemEmptyingDesk.On(source).EmptyAsync(Host.CuratorId, new ItemEmptyingDesk.EmptyingRequest(1, item, motive, protocol), CancellationToken.None);
        if (result.Outcome != EmptyingOutcome.Emptied)
        {
            throw new InvalidOperationException($"The emptying of item {item} ended in {result.Outcome}.");
        }
    }

    /// <summary>The events of the chain about the emptying of the item, each as the step, the reason, the protocol and the name of the person, in the order of the chain.</summary>
    public Task<IReadOnlyList<string>> EventsOfAsync(int item) =>
        Host.Database.FirstColumnAsOwnerAsync(
            "SELECT e.step || '|' || e.reason || '|' || e.reference || '|' || COALESCE(p.name, '') FROM public.custody_event e "
            + $"LEFT JOIN public.person p ON p.id = e.actor_person_id WHERE e.item_id = {item} AND e.step LIKE 'item_empt%' ORDER BY e.sequence");

    /// <summary>How many events the chain holds, of any kind.</summary>
    public async Task<int> ChainLengthAsync() => int.Parse((await Host.Database.FirstColumnAsOwnerAsync("SELECT count(*)::text FROM public.custody_event")).Single(), CultureInfo.InvariantCulture);

    public ValueTask DisposeAsync() => Host.DisposeAsync();

    private static string ContentOf(int domain, int count) =>
        "INSERT INTO content.item_text (domain_id, batch_id, raw_text, sanitized_text, pseudonymized_text, dek_epoch, dek_wrapped) "
        + $"SELECT {domain}, {domain}, '\x01', '\x02', 'zqx', 1, '\x03' FROM generate_series(1, {count})";

    private static Task SeedAsync(ScratchDatabase database) =>
        database.RunAsOwnerAsync(
            [
                $"INSERT INTO public.domain (name) VALUES ('{Domain}'), ('{OtherDomain}')",
                .. SeededBatches.Statements(1, 2),
                "SELECT set_config('app.domain', '1', false)",
                ContentOf(1, Items),
                "SELECT set_config('app.domain', '2', false)",
                ContentOf(2, 1),
                "SELECT set_config('app.domain', '', false)",
            ]);

    /// <summary>The source of grants of the test: the first domain to the person of the curation and none to anyone else.</summary>
    private sealed class GrantedToTheCurator(NpgsqlDataSource source, string curator) : DomainGrants
    {
        internal override async Task<IEnumerable<string>> GrantedToAsync(long entered, CancellationToken cancellationToken)
        {
            await using var command = source.CreateCommand("SELECT name FROM public.person WHERE id = @id");
            _ = command.Parameters.AddWithValue("id", entered);
            return string.Equals((string?)await command.ExecuteScalarAsync(cancellationToken), curator, StringComparison.Ordinal) ? Enumerable.Repeat(Domain, 1) : None;
        }
    }
}
