using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Core.Host.Endpoints;
using Core.Ingestion.Contracts.Reversal;
using Core.Persistence.Content;
using Core.Persistence.Content.Writing;
using Core.Persistence.Custody;
using Core.Persistence.Custody.Chain;
using Core.Persistence.People;
using Core.Persistence.Tests.Fixtures;
using Core.Platform.Keys;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Core.Host.Tests.Browser.Fixtures;

/// <summary>
/// The case of the screens of the reversal of a marker (issues 614 and 615) with the real routes and the desk the root registers, over a database with one domain, <see cref="Domain"/>, granted to
/// the person of the curation of the host and to a second one, <see cref="Second"/>, who decides what the first asks. The key is synthetic and holds the labels of the raw text and of the map of
/// the domain; each item is written as the pseudonymization writes it, with one marker of its own.
/// </summary>
internal sealed class ReversalCase : IAsyncDisposable
{
    /// <summary>The domain both people of the curation have granted, which is the active one.</summary>
    public const string Domain = "zqx-dominio-a";

    private static readonly string[] Labels = ["texto-bruto/1/v1", "mapa-reversao/1/v1"];

    private ReversalCase(ScreenHost host, string second)
    {
        Host = host;
        Second = second;
    }

    public ScreenHost Host { get; }

    /// <summary>The other person of the curation of the domain, with the password of the run.</summary>
    public string Second { get; }

    [SuppressMessage("Reliability", "CA2000", Justification = "The host goes to the case that is returned, which disposes it.")]
    public static async Task<ReversalCase> StartAsync(CancellationToken cancellationToken)
    {
        var second = $"zqx-curadoria-2-{Guid.NewGuid():N}";
        var host = await ScreenHost.StartAsync(
            new ScreenParts
            {
                Services = (services, setup) =>
                    _ = services.AddSingleton<DomainGrants>(provider => new GrantedToTheCuration(provider.GetRequiredService<NpgsqlDataSource>(), [setup.People.Curator, second]))
                        .AddSingleton(provider => new ServiceCollection().AddSingleton(provider.GetRequiredService<NpgsqlDataSource>())
                            .AddSingleton<IMasterKeySource>(new SyntheticKey()).BuildServiceProvider()),
                Screens = BatchRoutes.Map,
                Prepare = SeedAsync,
                Finish = started => started.AddPersonAsync(second, PersonFunctions.Curation, cancellationToken),
            },
            cancellationToken);
        return new ReversalCase(host, second);
    }

    /// <summary>A pseudonymized item of the domain with one marker of its own: its identifier and the marker.</summary>
    public async Task<(long Item, string Marker)> ItemAsync()
    {
        var marker = ItemTexts.Marker();
        var item = ItemTexts.Of("zqx-bruto", "zqx-limpo", $"zqx {marker} zqx") with { Map = [new NewMapEntry(marker, "zqx-original-sintetico", false, OccurrenceDestination.Pseudonymize)] };
        using var reading = await new SyntheticKey().ReadAsync(CancellationToken.None);
        await using var source = NpgsqlDataSource.Create(Host.Database.AppConnectionString);
        var written = await new ContentWriter(source, CompositionRoot.ContentOf(reading, new HashSet<string>(Labels, StringComparer.Ordinal))).AddPseudonymizedItemAsync(1, 1, item, CancellationToken.None);
        return (written.ItemId ?? throw new InvalidOperationException("The item was not written."), marker);
    }

    /// <summary>A request of the person of the curation of the host over a new item, asked as the screen asks it, for the second person to decide.</summary>
    public async Task<long> AskedAsync()
    {
        var (item, marker) = await ItemAsync();
        using var access = await Host.Services.GetRequiredService<ReversalDesk>().OpenAsync(CancellationToken.None) ?? throw new InvalidOperationException("The key was not read.");
        var draft = new ReversalRequestDraft { Item = item.ToString(CultureInfo.InvariantCulture), Marker = marker, Term = "30", Reason = "zqx-motivo-sintetico", Use = "zqx-uso-sintetico" };
        var asked = await access.Requests.AskAsync(1, Host.CuratorId, draft, CancellationToken.None);
        return asked.Id ?? throw new InvalidOperationException($"The request ended in {asked.Outcome}.");
    }

    public ValueTask DisposeAsync() => Host.DisposeAsync();

    /// <summary>The domain, the labels the key holds, an accepted batch, the version of the steps and the rehearsal of the recovery.</summary>
    private static async Task SeedAsync(ScratchDatabase database)
    {
        await database.RunAsOwnerAsync(
            [
                $"INSERT INTO public.domain (name) VALUES ('{Domain}')",
                $"INSERT INTO public.derivation_label (label) VALUES {string.Join(", ", Labels.Select(label => $"('{label}')"))}",
                .. SeededBatches.Statements(1),
                "SELECT set_config('app.domain', '1', true)",
                ItemTexts.VersionRow(1),
            ]);
        await using var source = NpgsqlDataSource.Create(database.AppConnectionString);
        var rehearsed = await new CustodyChain(source).AppendAsync(CustodyEvent.RecoveryRehearsed(1, new string('0', 64)), CancellationToken.None);
        if (rehearsed.Outcome != CustodyOutcome.Appended)
        {
            throw new InvalidOperationException($"The rehearsal of the recovery was not appended: {rehearsed.Outcome}.");
        }
    }

    /// <summary>The key of the tests, read anew each time, as the root reads the master key for each act of the reversal.</summary>
    private sealed class SyntheticKey : IMasterKeySource
    {
        public string Origin => "synthetic";

        public Task<MasterKeyReading> ReadAsync(CancellationToken cancellationToken)
        {
            var key = Enumerable.Range(0, MasterKey.Length).Select(index => (byte)(0x5A ^ ((index * 29) + 3))).ToArray();
            var secret = Enumerable.Range(0, MasterKey.Length).Select(index => (byte)(0xA7 ^ ((index * 31) + 11))).ToArray();
            return Task.FromResult(MasterKeyReading.FromBytes(key, DerivationLabels.Of(Labels), CustodyEpochs.Of(new Dictionary<int, byte[]> { [1] = secret }), "synthetic"));
        }
    }

    /// <summary>The source of grants of the test: the domain to the people of the curation named and none to anyone else.</summary>
    private sealed class GrantedToTheCuration(NpgsqlDataSource source, IReadOnlyCollection<string> curation) : DomainGrants
    {
        internal override async Task<IEnumerable<string>> GrantedToAsync(long entered, CancellationToken cancellationToken)
        {
            await using var command = source.CreateCommand("SELECT name FROM public.person WHERE id = @id");
            _ = command.Parameters.AddWithValue("id", entered);
            return curation.Contains((string?)await command.ExecuteScalarAsync(cancellationToken) ?? string.Empty) ? Enumerable.Repeat(Domain, 1) : None;
        }
    }
}
