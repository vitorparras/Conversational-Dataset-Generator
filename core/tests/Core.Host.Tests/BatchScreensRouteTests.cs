using System.Diagnostics.CodeAnalysis;
using System.Net;
using Core.Host.Endpoints;
using Core.Host.Operators;
using Core.Persistence.Domains;
using Core.Persistence.Ingestion;
using Core.Persistence.Migrations;
using Core.Persistence.People;
using Core.Persistence.Tests.Fixtures;
using Core.Platform.Sessions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Core.Host.Tests;

/// <summary>
/// The screen of the batches (issue 608) behind the real gates and over a database of its own with two domains and one batch in each: the curation sees the batches of the domains granted
/// alone, with the domain and the person by name (CA-1); with no domain granted it sees the list empty with the motive, and no other function gets more than the 404 of an address that does
/// not exist (CA-3); and a page past the ceiling of the register is the last page, never an error.
/// </summary>
[Trait("Category", "Database")]
public sealed class BatchScreensRouteTests
{
    private const string Granted = "zqx-dominio-a";
    private const string Other = "zqx-dominio-b";
    private const string CuratorName = "zqx-curadora";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [DatabaseFact]
    public async Task TheCurationSeesTheBatchesOfTheDomainsGrantedAloneWithTheDomainAndThePersonByName()
    {
        await using var world = await World.StartAsync(grantsCurator: true);

        var page = await world.PageAsync("/lotes", world.Curator);

        Assert.Contains("Lote\u00A01", page, StringComparison.Ordinal);
        Assert.Contains(Granted, page, StringComparison.Ordinal);
        Assert.Contains(CuratorName, page, StringComparison.Ordinal);
        Assert.DoesNotContain("Lote\u00A02", page, StringComparison.Ordinal);
        Assert.DoesNotContain(Other, page, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task TheFilterOfTheAddressCutsTheListInTheServer()
    {
        await using var world = await World.StartAsync(grantsCurator: true);

        var page = await world.PageAsync("/lotes?estado=rejeitados", world.Curator);

        Assert.DoesNotContain("Lote\u00A01", page, StringComparison.Ordinal);
        Assert.Contains("Nenhum lote com esse filtro.", page, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task TheServerCutsThePageOfTwentyAndReadsOneMoreToOfferTheNextWhichHoldsTheRest()
    {
        await using var world = await World.StartAsync(grantsCurator: true, twentyMore: true);

        var first = await world.PageAsync("/lotes", world.Curator);
        var second = await world.PageAsync("/lotes?pagina=2", world.Curator);

        Assert.Equal(20, Rows(first));
        Assert.Contains("Lote\u00A022</th>", first, StringComparison.Ordinal);
        Assert.DoesNotContain("Lote\u00A01</th>", first, StringComparison.Ordinal);
        Assert.Contains("rel=\"next\"", first, StringComparison.Ordinal);
        Assert.Equal(1, Rows(second));
        Assert.Contains("Lote\u00A01</th>", second, StringComparison.Ordinal);
        Assert.Contains("rel=\"prev\"", second, StringComparison.Ordinal);
        Assert.DoesNotContain("rel=\"next\"", second, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task ThePeriodOfTheAddressReachesTheRegisterFromItsFirstDayAndUntilItsLast()
    {
        await using var world = await World.StartAsync(grantsCurator: true);

        var since = await world.PageAsync("/lotes?de=2000-01-01", world.Curator);
        var before = await world.PageAsync("/lotes?ate=2000-01-01", world.Curator);
        var after = await world.PageAsync("/lotes?de=2999-01-01", world.Curator);

        Assert.Contains("Lote\u00A01</th>", since, StringComparison.Ordinal);
        Assert.DoesNotContain("Lote\u00A01</th>", before, StringComparison.Ordinal);
        Assert.Contains("Nenhum lote com esse filtro.", before, StringComparison.Ordinal);
        Assert.DoesNotContain("Lote\u00A01</th>", after, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task WithNoDomainGrantedTheCurationSeesTheListEmptyWithTheMotiveAndNoFilter()
    {
        await using var world = await World.StartAsync(grantsCurator: false);

        var page = await world.PageAsync("/lotes", world.Curator);

        Assert.Contains("Você não tem concessão em nenhum domínio.", page, StringComparison.Ordinal);
        Assert.DoesNotContain("Lote\u00A01", page, StringComparison.Ordinal);
        Assert.DoesNotContain("<form", page, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task AnotherFunctionIsAnsweredAsForAnAddressThatDoesNotExist()
    {
        await using var world = await World.StartAsync(grantsCurator: true);
        var session = world.Host.Sessions.Open(world.Annotator);

        using var refused = await world.Host.GetAsync("/lotes", session, encrypted: true, Token);
        using var missing = await world.Host.GetAsync("/zqx-nao-existe-6080", session, encrypted: true, Token);

        Assert.Equal((HttpStatusCode.NotFound, HttpStatusCode.NotFound), (refused.StatusCode, missing.StatusCode));
    }

    [DatabaseFact]
    public async Task APagePastTheCeilingOfTheRegisterIsTheLastPageAndNeverAnError()
    {
        await using var world = await World.StartAsync(grantsCurator: true);

        var page = await world.PageAsync("/lotes?pagina=999999", world.Curator);

        Assert.Contains("Nenhum lote com esse filtro.", page, StringComparison.Ordinal);
        Assert.DoesNotContain("pagina=501", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithNoDatabaseDeclaredTheHostSaysItCannot()
    {
        await using var host = await SessionedHost.StartAsync(services => services, BatchScreensEndpoint.Map, Token);

        using var listed = await host.GetAsync("/lotes", host.Sessions.Open(7), encrypted: true, Token);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, listed.StatusCode);
    }

    private static int Rows(string page) => page.Split("<th scope=\"row\" role=\"rowheader\">Lote", StringSplitOptions.None).Length - 1;

    private sealed class World : IAsyncDisposable
    {
        private const string PersonOf = "SELECT id FROM public.person WHERE name = @name";

        /// <summary>Twenty batches more in the domain granted, after the one of the seed, so that the list fills a page and leaves one for the next.</summary>
        private static readonly string[] TwentyMore =
        [
            "ALTER TABLE content.ingestion_batch DISABLE TRIGGER batch_is_born_open",
            "ALTER TABLE content.ingestion_batch DISABLE TRIGGER batch_open_needs_event",
            "SELECT set_config('app.domain', '1', false)",
            "INSERT INTO content.ingestion_batch (domain_id, state, opened_by, condition, closed_at) SELECT 1, 'accepted', (SELECT min(id) FROM public.person), 'restrita', now() FROM generate_series(1, 20)",
            "SELECT set_config('app.domain', '', false)",
            "ALTER TABLE content.ingestion_batch ENABLE ALWAYS TRIGGER batch_is_born_open",
            "ALTER TABLE content.ingestion_batch ENABLE ALWAYS TRIGGER batch_open_needs_event",
        ];

        private readonly ScratchDatabase database;
        private readonly NpgsqlDataSource source;

        private World(ScratchDatabase database, NpgsqlDataSource source, SessionedHost host, long curator, long annotator)
        {
            this.database = database;
            this.source = source;
            Host = host;
            Curator = curator;
            Annotator = annotator;
        }

        internal SessionedHost Host { get; }

        internal long Curator { get; }

        internal long Annotator { get; }

        internal static async Task<World> StartAsync(bool grantsCurator, bool twentyMore = false)
        {
            var database = await ScratchDatabase.CreateAsync();
            _ = await database.MigrateAsync(EmbeddedMigrations.Load());
            await database.RunAsOwnerAsync(
                "INSERT INTO public.domain (name) VALUES ('zqx-dominio-a'), ('zqx-dominio-b')",
                "INSERT INTO public.person (name, function_name) VALUES ('zqx-curadora', 'Curadoria do acervo')",
                "INSERT INTO public.person (name, function_name) VALUES ('zqx-anotadora', 'Anotação')");
            await database.RunAsOwnerAsync(SeededBatches.Statements(1, 2));
            if (twentyMore)
            {
                await database.RunAsOwnerAsync(TwentyMore);
            }

            var (curator, annotator) = (await PersonIdAsync(database, CuratorName), await PersonIdAsync(database, "zqx-anotadora"));
            var source = NpgsqlDataSource.Create(database.AppConnectionString);
            var staff = new OperatorAdministration(
                new PersonRegistry(source),
                new PersonCredentials(source, new PasswordDerivation(1)),
                new SessionStore(new SessionLifetimes(TimeSpan.FromMinutes(30), TimeSpan.FromHours(8)), TimeProvider.System),
                new PersonNotices(source));
            var grants = new FilledGrants(grantsCurator ? curator : 0, Granted);
            var host = await SessionedHost.StartAsync(
                services => services.AddSingleton(staff).AddSingleton(new BatchRegister(source)).AddSingleton(new DomainListing(source)).AddSingleton<DomainGrants>(grants),
                routes =>
                {
                    ScreenGate.Use((IApplicationBuilder)routes);
                    BatchScreensEndpoint.Map(routes);
                },
                Token);
            return new World(database, source, host, curator, annotator);
        }

        internal async Task<string> PageAsync(string route, long person)
        {
            using var response = await Host.GetAsync(route, Host.Sessions.Open(person), encrypted: true, Token);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(Token));
        }

        public async ValueTask DisposeAsync()
        {
            await Host.DisposeAsync();
            await source.DisposeAsync();
            await database.DisposeAsync();
        }

        [SuppressMessage("Security", "CA2100", Justification = "The statement is a literal, never input.")]
        private static async Task<long> PersonIdAsync(ScratchDatabase database, string name)
        {
            await using var connection = new NpgsqlConnection(database.OwnerConnectionString);
            await connection.OpenAsync(Token);
            await using var command = new NpgsqlCommand(PersonOf, connection);
            _ = command.Parameters.AddWithValue("name", name);
            return (long)(await command.ExecuteScalarAsync(Token))!;
        }
    }

    private sealed class FilledGrants : DomainGrants
    {
        private readonly long person;
        private readonly string[] domains;

        internal FilledGrants(long person, params string[] domains)
        {
            this.person = person;
            this.domains = domains;
        }

        internal override Task<IEnumerable<string>> GrantedToAsync(long entered, CancellationToken cancellationToken) =>
            Task.FromResult(entered == person ? domains.AsEnumerable() : None);
    }
}
